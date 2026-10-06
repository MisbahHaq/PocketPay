import { api } from '@/lib/api'
import type { TokenResponse } from '@/types/api'

export async function login(email: string, password: string): Promise<TokenResponse> {
  const response = await api.post<TokenResponse>('/auth/login', { email, password })
  return response.data
}

export async function logout(refreshToken: string): Promise<void> {
  // Always 204, even for an unknown token, so a caller cannot use this to probe whether
  // a token is valid.
  await api.post('/auth/logout', { refreshToken })
}