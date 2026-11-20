import { apiPath, request, setAuthToken } from '../../shared/api/httpClient'

export async function registerAccount(payload: {
  nickname: string
  email: string
  password: string
  confirmPassword: string
  // birthday 是完整的出生年月日（yyyy-MM-dd）。birthYear 是同一個日期的年份，
  // 只為了還沒更新的後端留著——它只認得年次那一欄。
  birthYear: string
  birthday: string
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

/**
 * 使用者在確認畫面（ConsentScreen）給的兩樣東西：同意條款、出生年月日（yyyy-MM-dd）。
 *
 * 兩個地方會送它：
 * · 第一次用社群帳號登入。社群登入的端點同時是登入也是註冊——帳號已經存在就直接登入，
 *   不需要這個；帳號不存在而又沒帶，後端回 SOCIAL_CONSENT_REQUIRED，前端顯示確認畫面後，
 *   拿同一張憑證加上這個再打一次。
 * · 既有帳號登入後的補問（giveConsent）。
 */
export type ConsentAnswer = { termsAccepted: true; birthday: string }

/** 後端說「這是新帳號，要先同意條款並確認年齡」。 */
export const SOCIAL_CONSENT_REQUIRED = 'SOCIAL_CONSENT_REQUIRED'

/**
 * 這個帳號還缺不缺條款同意與年齡確認。
 * 會缺的是以前用 Google／Facebook 建的帳號——那條路當時什麼都沒問。
 */
export async function getConsentStatus() {
  const response = await request<{ required: boolean }>('/api/account/consent')
  return response.data
}

export async function giveConsent(answer: ConsentAnswer) {
  const response = await request<{ required: boolean }>('/api/account/consent', {
    method: 'POST',
    body: JSON.stringify(answer),
  })
  return response.data
}

export async function googleLogin(idToken: string, remember: boolean, consent?: ConsentAnswer) {
  const response = await request<{ userId: string; nickname: string; email: string; token?: string }>('/api/auth/google', {
    method: 'POST',
    body: JSON.stringify({ idToken, ...consent }),
  })
  if (response.data.token) setAuthToken(response.data.token, remember)
  return response.data
}

export async function facebookLogin(accessToken: string, remember: boolean, consent?: ConsentAnswer) {
  const response = await request<{ userId: string; nickname: string; email: string; token?: string }>('/api/auth/facebook', {
    method: 'POST',
    body: JSON.stringify({ accessToken, ...consent }),
  })
  if (response.data.token) setAuthToken(response.data.token, remember)
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
  return request<boolean>(apiPath`/api/account/blocks/${blockedId}`, { method: 'DELETE' })
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
