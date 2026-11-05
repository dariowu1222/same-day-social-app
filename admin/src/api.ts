// 後台的 API 存取層。與使用者端 App 共用同一個後端，但登入是兩段式的：
//
//   1. Email + 密碼 → 換到一顆「一般 App token」。這顆 token 打不開任何後台端點，
//      而且刻意只留在記憶體裡，不寫進 sessionStorage。
//   2. 用那顆 token 要一組寄到信箱的驗證碼 → 驗過之後才換到「後台 token」。
//      只有這顆會被存起來，也只有它的 scope 能通過後端的 AdminSessionFilter。
//
// 所以管理者手機上那顆登入 App 用的 token 就算外流，也進不了後台。
// 後端對非管理者一律回 404，連端點存在都不揭露。

const TOKEN_KEY = 'admin_token'

// 第一段拿到的 App token。只在登入流程中間存在，拿到後台 token 就丟掉。
let pendingAppToken: string | null = null

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

async function request<T>(path: string, init?: RequestInit, useAppToken = false): Promise<T> {
  const token = useAppToken ? pendingAppToken : getToken()
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
    // 讓 App 把畫面退回登入頁。閒置逾時與被撤銷都會走到這裡，
    // 使用者看到的應該是「請重新登入」，不是滿畫面的錯誤。
    window.dispatchEvent(new CustomEvent('admin-session-expired'))
    throw new ApiError('後台登入已過期，請重新登入。', 401)
  }
  // 後端對「沒有後台權限」與「拿的不是後台 token」都回 404——它刻意不區分，
  // 不對外洩漏端點存在。從後台的角度，這兩種情況的處置都一樣：
  // 手上這顆 token 沒用，退回登入頁重走一次。
  if (res.status === 404 && (path.startsWith('/api/admin') || path.startsWith('/api/moderation'))) {
    clearToken()
    window.dispatchEvent(new CustomEvent('admin-session-expired'))
    throw new ApiError('後台登入已失效或這個帳號沒有權限，請重新登入。', 404)
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

/**
 * 第一段：Email + 密碼。
 *
 * 拿到的 App token 不存進 sessionStorage——它沒有後台權限，存著只是多一份外洩風險。
 * 回傳的 maskedEmail 讓使用者確認驗證碼會寄到哪裡。
 */
export async function loginStart(email: string, password: string): Promise<{ user: AuthUser; maskedEmail: string }> {
  const user = await request<AuthUser>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
  pendingAppToken = user.token ?? null
  if (!pendingAppToken) {
    throw new ApiError('登入回應缺少憑證。', 500)
  }

  const challenge = await request<{ maskedEmail: string }>(
    '/api/admin/auth/challenge', { method: 'POST' }, true)
  return { user, maskedEmail: challenge.maskedEmail }
}

/** 第二段：輸入信箱收到的驗證碼，換到真正的後台 token。 */
export async function loginVerify(code: string): Promise<void> {
  const result = await request<{ token: string; expiresAt: string }>(
    '/api/admin/auth/verify', { method: 'POST', body: JSON.stringify({ code }) }, true)
  setToken(result.token)
  pendingAppToken = null
}

/** 驗證碼沒收到時重寄一次。 */
export async function resendCode(): Promise<string> {
  const challenge = await request<{ maskedEmail: string }>(
    '/api/admin/auth/challenge', { method: 'POST' }, true)
  return challenge.maskedEmail
}

/** 破壞性動作（封鎖／註銷）前的再驗證。 */
export async function reverify(code: string): Promise<void> {
  await request<boolean>('/api/admin/auth/reverify', {
    method: 'POST',
    body: JSON.stringify({ code }),
  })
}

/**
 * 要一組「再驗證」專用的碼。
 *
 * 和登入用的碼是不同用途、互不通用：登入收到的碼不能拿來確認封鎖，
 * 反之亦然。信件內容也會寫明這組碼能做什麼。
 */
export async function requestReverifyCode(): Promise<string> {
  const challenge = await request<{ maskedEmail: string }>(
    '/api/admin/auth/reverify-challenge', { method: 'POST' })
  return challenge.maskedEmail
}

/** 登出：後端把 session 撤銷，那顆 token 當場失效，不是只有前端忘記它。 */
export async function logout(): Promise<void> {
  try {
    // keepalive：使用者按完登出就關分頁是常態，沒有它瀏覽器會把這發請求砍掉，
    // 於是後端的 session 根本沒被撤銷——那顆 token 還活著到過期為止。
    await request<boolean>('/api/admin/auth/logout', { method: 'POST', keepalive: true })
  } catch { /* 已經失效就算了，重點是本機要清乾淨 */ }
  clearToken()
  pendingAppToken = null
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

// ── AI 用量 ──────────────────────────────────────────

/** 一段期間的彙總。金額是後端自己乘出來的估算值，單位美元。 */
export type AiUsageBucket = {
  calls: number
  failedCalls: number
  inputTokens: number
  outputTokens: number
  reasoningTokens: number
  costUsd: number
}

export type AiUsageSummary = {
  /** 後端有沒有設 Ai:ApiKey。false 代表整條分析是關的，數字再漂亮也只是歷史資料。 */
  configured: boolean
  /** ai_usage_logs 這張表在不在。SQL 要人工貼到 Supabase 跑，忘了就是 false。 */
  storageReady: boolean
  model: string
  inputPricePerMTok: number
  cachedPricePerMTok: number
  outputPricePerMTok: number
  today: AiUsageBucket
  last7Days: AiUsageBucket
  thisMonth: AiUsageBucket
  allTime: AiUsageBucket
  averageLatencyMs: number
  lastCallAt?: string | null
}

export type AiUsageEntry = {
  id: string
  todayEntryId?: string | null
  model: string
  /** OK / TRUNCATED / REFUSED / EMPTY_CONTENT / PARSE_FAILED / TIMEOUT / ERROR */
  status: string
  inputTokens: number
  cachedInputTokens: number
  outputTokens: number
  reasoningTokens: number
  costUsd: number
  latencyMs: number
  error?: string | null
  createdAt: string
}

export type PagedAiUsage = { items: AiUsageEntry[]; total: number; page: number; pageSize: number }

export const getAiUsage = () => request<AiUsageSummary>('/api/admin/ai-usage')

export function getAiUsageLogs(params: { status?: string; page?: number; pageSize?: number }) {
  const q = new URLSearchParams()
  if (params.status) q.set('status', params.status)
  q.set('page', String(params.page ?? 1))
  q.set('pageSize', String(params.pageSize ?? 20))
  return request<PagedAiUsage>(`/api/admin/ai-usage/logs?${q}`)
}

// ── 規則設定 ─────────────────────────────────────────

/**
 * 營運規則，以及每個值的合法範圍。
 *
 * 上下限一起回是後端刻意的：驗證的真相只有一份。前端自己抄一份數字的話，
 * 哪天後端放寬了，畫面會繼續擋著不給改。
 */
export type AppSettings = {
  chatIdleDays: number
  chatIdleDaysMin: number
  chatIdleDaysMax: number
  chatMaxPartners: number
  chatMaxPartnersMin: number
  chatMaxPartnersMax: number
  /** 既有聊天室的寬限起點，後台唯讀。沒設就是沒有下限。 */
  chatExpirySince?: string | null
}

/** 跑完一次到期處理的結果。storageReady 為 false 代表 migration 還沒跑，什麼都沒做。 */
export type ChatExpiryOutcome = {
  notified: number
  expired: number
  scanned: number
  storageReady: boolean
}

export const getAppSettings = () => request<AppSettings>('/api/admin/settings')

/** 兩個欄位都是選填，只送有動到的那個——沒改的值送過去會多留一筆審核軌跡。 */
export const updateAppSettings = (patch: { chatIdleDays?: number; chatMaxPartners?: number }) =>
  request<AppSettings>('/api/admin/settings', { method: 'PUT', body: JSON.stringify(patch) })

export const runChatExpiry = () =>
  request<ChatExpiryOutcome>('/api/admin/settings/run-chat-expiry', { method: 'POST' })
