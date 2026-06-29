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
