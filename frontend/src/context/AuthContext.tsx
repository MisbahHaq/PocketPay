import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'

import { api, registerSessionLostHandler } from '@/lib/api'
import { login as loginRequest, logout as logoutRequest } from '@/lib/auth/service'
import {
  clearSession,
  readSession,
  refreshToken as storedRefreshToken,
  writeSession,
} from '@/lib/auth/storage'
import type { CurrentUser } from '@/types/api'

interface AuthContextValue {
  user: CurrentUser | null
  isAuthenticated: boolean
  /** True while the stored session is being checked on first load. */
  isInitialising: boolean
  signIn: (email: string, password: string) => Promise<void>
  signOut: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [isInitialising, setIsInitialising] = useState(true)

  // Restore the session on load. The user object is not persisted, only the tokens, so
  // it is recovered from the access token rather than trusted from storage.
  useEffect(() => {
    let cancelled = false

    async function restore() {
      const session = readSession()

      if (!session) {
        if (!cancelled) setIsInitialising(false)
        return
      }

      try {
        const response = await api.get<CurrentUser>('/auth/me')
        if (!cancelled) setUser(response.data)
      } catch {
        // The stored token is no longer usable. Clearing here rather than leaving it
        // means a stale token is not retried on every later request.
        clearSession()
        if (!cancelled) setUser(null)
      } finally {
        if (!cancelled) setIsInitialising(false)
      }
    }

    void restore()

    return () => {
      cancelled = true
    }
  }, [])

  // When the API client gives up on refreshing, drop the user here so the router sends
  // them to the login page instead of showing a shell full of failed requests.
  useEffect(() => {
    registerSessionLostHandler(() => setUser(null))
  }, [])

  const signIn = useCallback(async (email: string, password: string) => {
    const tokens = await loginRequest(email, password)

    writeSession({
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      expiresAt: tokens.expiresAt,
    })

    setUser(tokens.user)
  }, [])

  const signOut = useCallback(async () => {
    const token = storedRefreshToken()

    // Clear locally first. A failed logout call must not leave the user apparently
    // signed in on this device.
    clearSession()
    setUser(null)

    if (token) {
      try {
        await logoutRequest(token)
      } catch {
        // Already signed out locally; the server-side token expires on its own.
      }
    }
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      isInitialising,
      signIn,
      signOut,
    }),
    [user, isInitialising, signIn, signOut],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)

  if (!context) {
    throw new Error('useAuth must be used inside an AuthProvider')
  }

  return context
}