// 後台的 API 存取層。與使用者端 App 共用同一個後端，
// 但所有 /api/admin 與 /api/moderation 端點都由後端的白名單守門，
// 非管理者一律回 404（連端點存在都不揭露）。

const TOKEN_KEY = 'admin_token'

export type ApiResponse<T> = {
  success: boolean
  code?: string | null
  message?: string | null
  data?: T
}

export function getToken(): string | null {
  try {
    return sessionStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

// 刻意用 sessionStorage 而非 localStorage：
// 後台能看到使用者私訊與個資，關掉分頁就登出，降低共用電腦上的殘留風險。
export function setToken(token: string) {
  try {
    sessionStorage.setItem(TOKEN_KEY, token)
  } catch { /* 無痕模式等情境下忽略 */ }
}

export function clearToken() {
  try {
    sessionStorage.removeItem(TOKEN_KEY)
  } catch { /* ignore */ }
}

export class ApiError extends Error {
  // 不用建構子參數屬性：專案開了 erasableSyntaxOnly，那個語法無法單純抹除型別
  readonly status: number
  readonly code?: string | null

  constructor(message: string, status: number, code?: string | null) {
    super(message)
    this.status = status
    this.code = code
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken()
  const res = await fetch(path, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers,
    },
  })

  if (res.status === 401) {
    clearToken()
    throw new ApiError('登入已失效，請重新登入。', 401)
  }
  // 後端對非管理者一律回 404，這裡翻譯成看得懂的訊息
  if (res.status === 404 && path.startsWith('/api/admin')) {
    throw new ApiError('這個帳號沒有後台權限。', 404)
  }

  let body: ApiResponse<T> | null = null
  try {
    body = await res.json()
  } catch { /* 非 JSON 回應 */ }

  if (!res.ok || body?.success === false) {
    throw new ApiError(body?.message ?? `請求失敗（${res.status}）`, res.status, body?.code)
  }
  return body?.data as T
}

// ── 登入 ─────────────────────────────────────────────

export type AuthUser = { userId: string; nickname: string; email: string; token?: string }

export async function login(email: string, password: string): Promise<AuthUser> {
  const user = await request<AuthUser>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
  if (user.token) setToken(user.token)
  return user
}

// ── 儀表板 ───────────────────────────────────────────

export type Dashboard = {
  totalUsers: number
  newUsersToday: number
  suspendedUsers: number
  bannedUsers: number
  pendingReports: number
  highSeverityReports: number
}

export const getDashboard = () => request<Dashboard>('/api/admin/dashboard')

// ── 會員 ─────────────────────────────────────────────

export type AccountStatus = 'ACTIVE' | 'SUSPENDED' | 'BANNED' | 'DELETED'
export type MembershipTier = 'FREE' | 'PREMIUM' | 'VIP'

export type UserSummary = {
  id: string
  nickname: string
  status: AccountStatus
  membershipTier: MembershipTier
  emailVerified: boolean
  identityVerified: boolean
  suspendedUntil?: string | null
  createdAt: string
  lastLoginAt?: string | null
  reportsAgainst: number
}

export type SanctionEntry = {
  action: string
  reason?: string | null
  expiresAt?: string | null
  actorId: string
  createdAt: string
}

export type ReportEntry = {
  id: string
  targetType: string
  targetId: string
  category: string
  severity: number
  status: ReportStatus
  detail?: string | null
  contextSnapshot?: string | null
  createdAt: string
  reviewedAt?: string | null
  resolution?: string | null
}

export type UserDetail = UserSummary & {
  gender?: string | null
  ageRange?: string | null
  bio: string
  locationArea?: string | null
  occupation?: string | null
  school?: string | null
  statusReason?: string | null
  membershipExpiresAt?: string | null
  phoneVerified: boolean
  username?: string | null
  postCount: number
  replyCount: number
  messageCount: number
  sanctions: SanctionEntry[]
  reports: ReportEntry[]
}

export type PagedUsers = { items: UserSummary[]; total: number; page: number; pageSize: number }

export function searchUsers(params: {
  keyword?: string
  status?: AccountStatus | ''
  page?: number
  pageSize?: number
}) {
  const q = new URLSearchParams()
  if (params.keyword) q.set('keyword', params.keyword)
  if (params.status) q.set('status', params.status)
  q.set('page', String(params.page ?? 1))
  q.set('pageSize', String(params.pageSize ?? 20))
  return request<PagedUsers>(`/api/admin/users?${q}`)
}

export const getUserDetail = (id: string) => request<UserDetail>(`/api/admin/users/${id}`)

export const updateUser = (id: string, patch: {
  nickname?: string
  bio?: string
  membershipTier?: MembershipTier
  membershipExpiresAt?: string | null
}) => request<boolean>(`/api/admin/users/${id}`, { method: 'PUT', body: JSON.stringify(patch) })

export type SanctionAction = 'SUSPEND' | 'UNSUSPEND' | 'BAN' | 'UNBAN' | 'DELETE'

export const sanctionUser = (id: string, body: {
  action: SanctionAction
  reason?: string
  expiresAt?: string | null
  reportId?: string | null
}) => request<boolean>(`/api/admin/users/${id}/sanction`, { method: 'POST', body: JSON.stringify(body) })

// ── 檢舉 ─────────────────────────────────────────────

export type ReportStatus = 'PENDING' | 'REVIEWING' | 'RESOLVED' | 'DISMISSED'

export type ModerationReport = {
  id: string
  targetType: string
  targetId: string
  category: string
  severity: number
  status: ReportStatus
  detail?: string | null
  contextSnapshot?: string | null
  reportedUserId: string
  reportsAgainstUser: number
  createdAt: string
  reviewedBy?: string | null
  reviewedAt?: string | null
  resolution?: string | null
}

export const getReports = (status: ReportStatus | '', limit = 50) => {
  const q = new URLSearchParams()
  if (status) q.set('status', status)
  q.set('limit', String(limit))
  return request<ModerationReport[]>(`/api/moderation/reports?${q}`)
}

export const reviewReport = (id: string, body: {
  status: ReportStatus
  resolution?: string
  hideTarget?: boolean | null
}) => request<boolean>(`/api/moderation/reports/${id}/review`, {
  method: 'POST',
  body: JSON.stringify(body),
})
