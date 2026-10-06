/**
 * Token storage and refresh, deliberately kept out of the axios instance.
 *
 * Both live here so the refresh interceptor below can call them without a circular
 * import, and so that `api.ts` stays about transport.
 */

const ACCESS_TOKEN_KEY = 'inventory.accessToken'
const REFRESH_TOKEN_KEY = 'inventory.refreshToken'
const EXPIRES_AT_KEY = 'inventory.expiresAt'

export interface StoredSession {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

/**
 * Reads the stored session.
 *
 * Returns null when anything is missing or unparseable rather than throwing: a corrupt
 * value must degrade to "signed out", not to a blank screen on every page load.
 */
export function readSession(): StoredSession | null {
  try {
    const accessToken = localStorage.getItem(ACCESS_TOKEN_KEY)
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)
    const expiresAt = localStorage.getItem(EXPIRES_AT_KEY)

    if (!accessToken || !refreshToken || !expiresAt) {
      return null
    }

    return { accessToken, refreshToken, expiresAt }
  } catch {
    // Private browsing modes and blocked storage both throw on access.
    return null
  }
}

export function writeSession(session: StoredSession): void {
  try {
    localStorage.setItem(ACCESS_TOKEN_KEY, session.accessToken)
    localStorage.setItem(REFRESH_TOKEN_KEY, session.refreshToken)
    localStorage.setItem(EXPIRES_AT_KEY, session.expiresAt)
  } catch {
    // A session that cannot be persisted is still usable for this tab; the next reload
    // simply signs the user out again.
  }
}

export function clearSession(): void {
  try {
    localStorage.removeItem(ACCESS_TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    localStorage.removeItem(EXPIRES_AT_KEY)
  } catch {
    // Nothing useful to do; the in-memory state is cleared regardless.
  }
}

export function accessToken(): string | null {
  return readSession()?.accessToken ?? null
}

export function refreshToken(): string | null {
  return readSession()?.refreshToken ?? null
}

/**
 * Whether the access token is expired or close enough to expiry to be worth replacing.
 *
 * The margin is deliberate: refreshing only *after* a 401 costs a wasted round trip and
 * a visible failed request. Proactive refresh smooths that out.
 */
export function isAccessTokenExpiring(skewSeconds = 60): boolean {
  const session = readSession()

  if (!session) {
    return true
  }

  const expiresAt = Date.parse(session.expiresAt)

  if (Number.isNaN(expiresAt)) {
    return true
  }

  return expiresAt - Date.now() <= skewSeconds * 1000
}