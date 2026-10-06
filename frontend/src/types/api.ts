/** Shapes returned by the .NET API. Mirrors backend/src/Inventory.Api contracts. */

export const SystemRoles = {
  Admin: 'Admin',
  Manager: 'Manager',
  Staff: 'Staff',
} as const

export type SystemRole = (typeof SystemRoles)[keyof typeof SystemRoles]

export interface CurrentUser {
  id: string
  email: string
  fullName: string
  roles: string[]
}

export interface TokenResponse {
  accessToken: string
  tokenType: string
  expiresInSeconds: number
  expiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  user: CurrentUser
}

/**
 * RFC 7807 response body. Only the fields the UI actually branches on are typed; the API
 * may add more without breaking the client.
 */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  /** Keyed by field name, so a form can highlight the offending input. */
  errors?: Record<string, string[]>
}

export function hasRole(user: CurrentUser | null, role: SystemRole): boolean {
  return user?.roles.includes(role) ?? false
}

/** Admin is a superset of the other two for navigation purposes. */
export function canManageUsers(user: CurrentUser | null): boolean {
  return hasRole(user, SystemRoles.Admin)
}

export function canManageCatalog(user: CurrentUser | null): boolean {
  return hasRole(user, SystemRoles.Admin) || hasRole(user, SystemRoles.Manager)
}