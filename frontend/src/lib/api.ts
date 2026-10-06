import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'

import {
  clearSession,
  isAccessTokenExpiring,
  accessToken,
  refreshToken,
  writeSession,
} from '@/lib/auth/storage'
import type { ProblemDetails, TokenResponse } from '@/types/api'

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api/v1'

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 15_000,
})

/** Set by the auth provider so the interceptor can react to a failed refresh. */
let onSessionLost: (() => void) | null = null

export function registerSessionLostHandler(handler: () => void): void {
  onSessionLost = handler
}

/* -------------------------------------------------------------------------- */
/* Request: attach the access token, refreshing first when it is about to expire */
/* -------------------------------------------------------------------------- */

api.interceptors.request.use(async (config: InternalAxiosRequestConfig) => {
  // Never let the refresh call try to refresh itself.
  if (config.url?.includes('/auth/refresh') || config.url?.includes('/auth/login')) {
    return config
  }

  if (isAccessTokenExpiring() && refreshToken()) {
    const refreshed = await tryRefresh()
    if (!refreshed) {
      return Promise.reject(new SessionExpiredError())
    }
    config.headers.Authorization = `Bearer ${accessToken()}`
    return config
  }

  const token = accessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

/* -------------------------------------------------------------------------- */
/* Response: on 401, refresh once and replay the original request                 */
/* -------------------------------------------------------------------------- */

interface RetriableRequest extends InternalAxiosRequestConfig {
  /** Marks a request that has already been replayed, so it cannot loop. */
  _retried?: boolean
}

export class SessionExpiredError extends Error {
  constructor() {
    super('Your session has expired. Please sign in again.')
    this.name = 'SessionExpiredError'
  }
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ProblemDetails>) => {
    const original = error.config as RetriableRequest | undefined

    const isUnauthorized = error.response?.status === 401
    const isAuthEndpoint =
      original?.url?.includes('/auth/login') === true ||
      original?.url?.includes('/auth/refresh') === true

    // Without this guard, a permanently rejected token would loop: 401, refresh, 401,
    // refresh, until the tab hung or the rate limiter intervened.
    if (!isUnauthorized || !original || original._retried || isAuthEndpoint) {
      throw toFriendlyError(error)
    }

    original._retried = true

    const refreshed = await tryRefresh()

    if (!refreshed) {
      clearSession()
      onSessionLost?.()
      throw new SessionExpiredError()
    }

    original.headers.Authorization = `Bearer ${accessToken()}`

    return api.request(original)
  },
)

/* -------------------------------------------------------------------------- */
/* Refresh coordination                                                          */
/* -------------------------------------------------------------------------- */

let refreshInFlight: Promise<boolean> | null = null

/**
 * Refreshes at most once at a time, shared by every waiting caller.
 *
 * Without this, five requests failing at once would fire five refresh calls. The API
 * rotates refresh tokens, so four of them would be replays - which the server treats as a
 * stolen token and answers by revoking the whole family, signing the user out.
 */
async function tryRefresh(): Promise<boolean> {
  if (!refreshInFlight) {
    refreshInFlight = performRefresh().finally(() => {
      refreshInFlight = null
    })
  }

  return refreshInFlight
}

async function performRefresh(): Promise<boolean> {
  const token = refreshToken()

  if (!token) {
    return false
  }

  try {
    // A bare axios call, not `api`: going through the instance would re-enter these
    // interceptors and recurse.
    const response = await axios.post<TokenResponse>(
      `${API_BASE_URL}/auth/refresh`,
      { refreshToken: token },
      { headers: { 'Content-Type': 'application/json' }, timeout: 15_000 },
    )

    writeSession({
      accessToken: response.data.accessToken,
      refreshToken: response.data.refreshToken,
      expiresAt: response.data.expiresAt,
    })

    return true
  } catch {
    // Expired, revoked, or replayed. The caller clears the session.
    return false
  }
}

/* -------------------------------------------------------------------------- */
/* Error shaping                                                                 */
/* -------------------------------------------------------------------------- */

/**
 * Error carrying per-field messages, so forms can show server-side validation next to
 * the input that caused it rather than as one opaque banner.
 */
export class ValidationError extends Error {
  constructor(
    readonly errors: Record<string, string[]>,
    message = 'Please correct the highlighted fields.',
  ) {
    super(message)
    this.name = 'ValidationError'
  }
}

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly traceId?: string,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

function toFriendlyError(error: AxiosError<ProblemDetails>): Error {
  const status = error.response?.status
  const body = error.response?.data

  if (body?.errors && Object.keys(body.errors).length > 0) {
    return new ValidationError(body.errors, body.detail ?? body.title)
  }

  if (status === undefined) {
    // No response at all: the API is not running, or CORS or DNS is in the way.
    return new ApiError(
      'Cannot reach the server. Check that the API is running and try again.',
      0,
    )
  }

  return new ApiError(
    body?.detail ?? body?.title ?? defaultMessageFor(status),
    status,
    body?.traceId,
  )
}

function defaultMessageFor(status: number): string {
  switch (status) {
    case 400:
      return 'The request was malformed.'
    case 401:
      return 'Your session has expired. Please sign in again.'
    case 403:
      return 'You do not have permission to do that.'
    case 404:
      return 'That item could not be found.'
    case 409:
      return 'That change conflicts with the current state. Reload and try again.'
    case 422:
      return 'Please correct the highlighted fields.'
    case 429:
      return 'Too many attempts. Please wait a moment and try again.'
    default:
      return status >= 500
        ? 'Something went wrong on the server. Please try again.'
        : 'The request could not be completed.'
  }
}