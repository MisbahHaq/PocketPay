import { useEffect, useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'

import { useAuth } from '@/context/AuthContext'

/**
 * Gate for authenticated routes.
 *
 * Waits for the session check before deciding, because redirecting during
 * initialisation would bounce a signed-in user to the login page on every full page load.
 */
export function RequireAuth({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isInitialising } = useAuth()
  const location = useLocation()

  if (isInitialising) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <p className="text-sm text-muted-foreground">Loading…</p>
      </div>
    )
  }

  if (!isAuthenticated) {
    // `state.from` lets the user return to where they were after signing in, rather than
    // always landing on the dashboard.
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  return <>{children}</>
}

/** Keeps a signed-in user off the login page. */
export function RedirectIfAuthenticated({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isInitialising } = useAuth()

  if (isInitialising) {
    return <div className="flex min-h-screen items-center justify-center" />
  }

  if (isAuthenticated) {
    return <Navigate to="/" replace />
  }

  return <>{children}</>
}

/** Tracks the current route for the sidebar highlight. */
export function useCurrentPath(): string {
  const location = useLocation()

  const [path, setPath] = useState(location.pathname)

  useEffect(() => {
    setPath(location.pathname)
  }, [location.pathname])

  return path
}