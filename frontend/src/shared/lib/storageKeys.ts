// 全專案唯一的 localStorage / sessionStorage key 來源。
// 禁止在其他檔案直接寫 'same-day-...' 字串字面值。
export const STORAGE_KEYS = {
  token: 'same-day-auth-token',
  user: 'same-day-demo-user',
} as const

export function onboardingKey(userId: string) {
  return `same-day-onboarding-${userId}`
}

// 安全提示「不再顯示」旗標（全域，一律 localStorage 持久）。
export const SAFETY_KEYS = {
  chatNotice: 'same-day-chat-safety-dismissed',
  meetup: 'same-day-meetup-safety-dismissed',
} as const

export function safetyDismissed(key: string): boolean {
  return localStorage.getItem(key) === '1'
}

export function dismissSafety(key: string) {
  localStorage.setItem(key, '1')
}

// 「記住我」開關：remember 時寫 localStorage（長期），否則寫 sessionStorage（關閉分頁即清）。
// 同時清掉另一邊，避免兩處殘留不一致。
export function rememberSet(key: string, value: string, remember: boolean) {
  if (remember) {
    localStorage.setItem(key, value)
    sessionStorage.removeItem(key)
  } else {
    sessionStorage.setItem(key, value)
    localStorage.removeItem(key)
  }
}

export function rememberGet(key: string): string | null {
  return localStorage.getItem(key) ?? sessionStorage.getItem(key)
}

export function rememberClear(key: string) {
  localStorage.removeItem(key)
  sessionStorage.removeItem(key)
}

/**
 * 今日頁進場動畫的狀態。每位使用者每天第一次進來才播。
 *
 * 先前存 sessionStorage，等於每次重開 App 都要再看一次 3.75 秒的打字機
 * （12 字 × 100ms + 1.2s 停頓 + 0.4s 游標淡出 + 0.95s 上飄）。
 *
 * 值存的是當天的本機日期而不是 '1'：同一位使用者永遠共用一把 key，
 * 不會每過一天就在 localStorage 多留一筆垃圾。
 *
 * 這裡直接讀 storage 取使用者，而不是從 AuthContext 拿——AuthProvider 是在
 * useEffect 裡還原登入狀態的，第一次 render 時 user 還是 null，
 * 等它的話回訪者會先閃一格動畫才消失。
 */
function todayIntroState(): { key: string; today: string } | null {
  const raw = rememberGet(STORAGE_KEYS.user)
  if (!raw) return null

  let userId = ''
  try {
    userId = (JSON.parse(raw) as { userId?: string }).userId ?? ''
  } catch {
    return null
  }
  if (!userId) return null

  // 本機日期，不用 UTC：使用者的「今天」是他手機上的今天。
  const now = new Date()
  const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
  return { key: `same-day-today-intro-${userId}`, today }
}

export function shouldPlayTodayIntro(): boolean {
  const state = todayIntroState()
  if (state === null) return false
  return localStorage.getItem(state.key) !== state.today
}

export function markTodayIntroSeen() {
  const state = todayIntroState()
  if (state !== null) localStorage.setItem(state.key, state.today)
}
