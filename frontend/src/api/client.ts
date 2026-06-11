const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

const TOKEN_KEY = 'same-day-auth-token'

export function getAuthToken(): string | null {
  return localStorage.getItem(TOKEN_KEY) ?? sessionStorage.getItem(TOKEN_KEY)
}

export function setAuthToken(token: string, remember: boolean) {
  if (remember) {
    localStorage.setItem(TOKEN_KEY, token)
    sessionStorage.removeItem(TOKEN_KEY)
  } else {
    sessionStorage.setItem(TOKEN_KEY, token)
    localStorage.removeItem(TOKEN_KEY)
  }
}

export function clearAuthToken() {
  localStorage.removeItem(TOKEN_KEY)
  sessionStorage.removeItem(TOKEN_KEY)
}

type ApiResponse<T> = {
  success: boolean
  code?: string
  message?: string
  warning?: string
  data: T
}

async function request<T>(path: string, options?: RequestInit): Promise<ApiResponse<T>> {
  let response: Response
  const token = getAuthToken()
  const authHeader: Record<string, string> = token ? { Authorization: `Bearer ${token}` } : {}
  try {
    const controller = new AbortController()
    const timer = setTimeout(() => controller.abort(), 30000)
    try {
      response = await fetch(`${API_BASE_URL}${path}`, {
        headers: { 'Content-Type': 'application/json', ...authHeader, ...(options?.headers ?? {}) },
        signal: controller.signal,
        ...options,
      })
    } finally {
      clearTimeout(timer)
    }
  } catch (err) {
    if (err instanceof DOMException && err.name === 'AbortError') {
      throw new Error('連線逾時，請確認後端服務是否已啟動。')
    }
    throw new Error('目前無法連線到伺服器，請確認後端是否已啟動。')
  }

  if (response.status === 401) {
    clearAuthToken()
    localStorage.removeItem('same-day-demo-user')
    sessionStorage.removeItem('same-day-demo-user')
    window.location.reload()
    throw new Error('登入已過期，請重新登入。')
  }

  let body: ApiResponse<T>
  try {
    body = (await response.json()) as ApiResponse<T>
  } catch {
    throw new Error(response.ok ? '伺服器回應格式異常，請稍後再試。' : '伺服器暫時無法處理，請稍後再試。')
  }

  if (!response.ok || !body.success) {
    throw new Error(toUserMessage(body.code, body.message))
  }
  return body
}

function toUserMessage(code?: string, message?: string) {
  switch (code) {
    case 'ACCOUNT_NOT_FOUND':
      return '查無此帳號，請確認 Email 是否輸入正確，或先建立帳號。'
    case 'INVALID_PASSWORD':
      return '密碼錯誤，請重新輸入。'
    case 'INVALID_LOGIN':
      return '請輸入正確的 Email 與密碼。'
    case 'DATABASE_NOT_CONFIGURED':
      return '目前尚未連接資料庫，無法使用正式登入。'
    case 'DATABASE_UNAVAILABLE':
      return '目前無法連線資料庫，請稍後再試。'
    case 'SMTP_NOT_CONFIGURED':
      return '目前尚未設定寄信服務，無法寄出驗證碼。'
    case 'TERMS_NOT_ACCEPTED':
      return '請先閱讀並同意服務條款與隱私權政策。'
    case 'UNDERAGE':
      return '未滿 18 歲暫時不能使用同頻 Today。'
    case 'INVALID_CODE':
      return '驗證碼錯誤或已過期，請確認後再輸入。'
    case 'EMAIL_SEND_FAILED':
      return '驗證碼寄送失敗，請稍後再試。'
    case 'ACCOUNT_EXISTS':
      return '這個 Email 已經註冊過，請直接登入或使用忘記密碼。'
    default:
      return message ?? '操作失敗，請稍後再試。'
  }
}

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

export async function demoLogin(nickname: string) {
  const response = await request<{ userId: string; nickname: string; token?: string }>('/api/auth/demo-login', {
    method: 'POST',
    body: JSON.stringify({ nickname }),
  })
  if (response.data.token) setAuthToken(response.data.token, false)
  return response.data
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

export async function getProfile(userId: string) {
  return request<UserProfile>(`/api/profile/${userId}`)
}

export async function updateProfile(userId: string, payload: {
  nickname?: string
  bio?: string
  interestTags?: string[]
  valueTags?: string[]
  photoDataUrls?: string[]
  birthday?: string
}) {
  return request<UserProfile>(`/api/profile/${userId}`, {
    method: 'PUT',
    body: JSON.stringify(payload),
  })
}

export type UserProfile = {
  id: string
  nickname: string
  bio: string
  interestTags: string[]
  valueTags: string[]
  photoDataUrls: string[]
  birthday?: string
  responsePreference?: string
}

export async function createTodayEntry(payload: {
  userId: string
  content: string
  responseMode: string
  visibility: string
}) {
  return request<{ entry: TodayEntry; analysis: TodayAnalysis }>('/api/today-entries', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export async function getTodayMatches(userId: string) {
  return request<MatchResult[]>(`/api/matches/today/${userId}`)
}

export async function getRants() {
  return request<RantPost[]>('/api/rants')
}

export async function createRant(payload: { nickname: string; content: string; mode: string; hashtags?: string[]; imageDataUrl?: string | null; audioDataUrl?: string | null }) {
  return request<RantPost>('/api/rants', { method: 'POST', body: JSON.stringify(payload) })
}

export async function understandRant(rantId: string) {
  return request<RantPost>(`/api/rants/${rantId}/understand`, { method: 'POST' })
}

export async function replyRant(rantId: string, payload: { nickname: string; content: string; imageDataUrl?: string | null; audioDataUrl?: string | null; parentReplyId?: string | null }) {
  return request<RantPost>(`/api/rants/${rantId}/replies`, { method: 'POST', body: JSON.stringify(payload) })
}

export async function likeReply(rantId: string, replyId: string) {
  return request<RantPost>(`/api/rants/${rantId}/replies/${replyId}/understand`, { method: 'POST' })
}

export async function reportRant(rantId: string) {
  return request<RantPost>(`/api/rants/${rantId}/report`, { method: 'POST' })
}

export async function deleteRant(rantId: string) {
  return request<boolean>(`/api/rants/${rantId}`, { method: 'DELETE' })
}

export async function getTasks() {
  return request<SocialTask[]>('/api/tasks')
}

export async function joinTask(taskId: string, userId: string) {
  return request<SocialTask>(`/api/tasks/${taskId}/join`, { method: 'POST', body: JSON.stringify({ userId }) })
}

export async function getChatRooms(userId: string) {
  return request<ChatRoom[]>(`/api/chats/user/${userId}`)
}

export async function createChatRoom(payload: { userIds: string[]; sourceType: string; sourceId: string }) {
  return request<ChatRoom>('/api/chats', { method: 'POST', body: JSON.stringify(payload) })
}

export async function getMessages(chatRoomId: string) {
  return request<ChatMessage[]>(`/api/chats/${chatRoomId}/messages`)
}

export async function sendMessage(chatRoomId: string, content: string) {
  return request<ChatMessage>(`/api/chats/${chatRoomId}/messages`, { method: 'POST', body: JSON.stringify({ content }) })
}

export type TodayAnalysis = {
  eventType: string
  emotionTags: string[]
  valueTags: string[]
  interestTags: string[]
  responseMode: string
}

export type TodayEntry = TodayAnalysis & {
  id: string
  userId: string
  content: string
  visibility: string
  createdAt: string
}

export type MatchResult = {
  matchId: string
  matchedUserId: string
  nickname: string
  matchScore: number
  matchType: string
  sharedTags: string[]
  reason: string
  icebreaker: string
  todaySummary: string
}

export type RantReply = {
  id: string
  nickname: string
  content: string
  imageDataUrl?: string | null
  audioDataUrl?: string | null
  likeCount: number
  parentReplyId?: string | null
  replies?: RantReply[]
}

export type RantPost = {
  id: string
  userId: string
  nickname: string
  content: string
  mode: string
  emotionTags: string[]
  hashtags: string[]
  imageDataUrl?: string | null
  audioDataUrl?: string | null
  createdAt: string
  likeCount: number
  replyCount: number
  reportCount: number
  replies: RantReply[]
}

export function flattenReplies(replies: RantReply[]): RantReply[] {
  return replies.flatMap((r) => [r, ...flattenReplies(r.replies ?? [])])
}

export type SocialTask = {
  id: string
  title: string
  description: string
  category: string
  duration: string
  difficulty: string
  participantLimit: number
  participantUserIds: string[]
}

export type ChatRoom = {
  id: string
  userIds: string[]
  sourceType: string
  sourceId: string
  createdAt: string
}

export type ChatMessage = {
  id: string
  chatRoomId: string
  senderId: string
  content: string
  createdAt: string
}
