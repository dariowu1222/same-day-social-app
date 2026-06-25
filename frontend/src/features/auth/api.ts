import { request, setAuthToken } from '../../shared/api/httpClient'

export async function registerAccount(payload: {
  nickname: string
  email: string
  password: string
  confirmPassword: string
  birthYear: string
  gender: string
  termsAccepted: boolean
}) {
  return request<{ email: string; expiresInMinutes: number }>('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export async function confirmRegistration(payload: { email: string; code: string }, remember: boolean) {
  const response = await request<{ userId: string; nickname: string; email: string; token?: string }>('/api/auth/register/confirm', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
  if (response.data.token) setAuthToken(response.data.token, remember)
  return response.data
}

export async function loginAccount(payload: { email: string; password: string }, remember: boolean) {
  const response = await request<{ userId: string; nickname: string; email: string; token?: string }>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
  if (response.data.token) setAuthToken(response.data.token, remember)
  return response.data
}

export async function googleLogin(idToken: string, remember: boolean) {
  const response = await request<{ userId: string; nickname: string; email: string; token?: string }>('/api/auth/google', {
    method: 'POST',
    body: JSON.stringify({ idToken }),
  })
  if (response.data.token) setAuthToken(response.data.token, remember)
  return response.data
}

export async function demoLogin(nickname: string) {
  const response = await request<{ userId: string; nickname: string; token?: string }>('/api/auth/demo-login', {
    method: 'POST',
    body: JSON.stringify({ nickname }),
  })
  if (response.data.token) setAuthToken(response.data.token, false)
  return response.data
}

export async function deleteAccount() {
  return request<boolean>('/api/account', { method: 'DELETE' })
}

export type BlockedUser = { userId: string; nickname: string; blockedAt: string }

export async function getBlockedUsers() {
  return request<BlockedUser[]>('/api/account/blocks')
}

export async function unblockUser(blockedId: string) {
  return request<boolean>(`/api/account/blocks/${blockedId}`, { method: 'DELETE' })
}

export type UserSetting = {
  profilePublic: boolean
  pauseMatching: boolean
  notifyMatch: boolean
  notifyMessage: boolean
  notifyRant: boolean
}

export async function getSettings() {
  return request<UserSetting>('/api/account/settings')
}

export async function updateSettings(patch: Partial<UserSetting>) {
  return request<UserSetting>('/api/account/settings', { method: 'PUT', body: JSON.stringify(patch) })
}

export async function requestPasswordReset(email: string) {
  return request<{ email: string; expiresInMinutes: number }>('/api/auth/password-reset/request', {
    method: 'POST',
    body: JSON.stringify({ email }),
  })
}

export async function verifyPasswordReset(payload: { email: string; code: string }) {
  return request<{ verified: boolean }>('/api/auth/password-reset/verify', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export async function confirmPasswordReset(payload: {
  email: string
  code: string
  newPassword: string
  confirmPassword: string
}) {
  const response = await request<{ userId: string; nickname: string; email: string }>('/api/auth/password-reset/confirm', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
  return response.data
}
