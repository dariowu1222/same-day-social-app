import { STORAGE_KEYS, rememberSet, rememberGet, rememberClear } from '../lib/storageKeys'
import { toUserMessage } from './errorMessages'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

export function getAuthToken(): string | null {
  return rememberGet(STORAGE_KEYS.token)
}

export function setAuthToken(token: string, remember: boolean) {
  rememberSet(STORAGE_KEYS.token, token, remember)
}

export function clearAuthToken() {
  rememberClear(STORAGE_KEYS.token)
}

export type ApiResponse<T> = {
  success: boolean
  code?: string
  message?: string
  warning?: string
  data: T
}

// Render 免費方案閒置 15 分鐘會休眠，喚醒實測 13.6 秒、官方說最長約一分鐘。
// 逾時原本寫死 30 秒，休眠後的第一個請求常常還沒醒就被中斷，使用者看到的
// 就是「傳送失敗」。放寬到 75 秒讓冷啟動跑得完。
// 沒網路、DNS 失敗這類連線層級的錯誤本來就會立刻 reject，不會白等 75 秒。
// 之後改成 Render 付費方案（不休眠）就可以把這兩個值調回 30 秒上下。
const FIRST_TIMEOUT_MS = 75000
// 重試時伺服器應該已經被前一次請求叫醒了，不需要再給一次冷啟動的時間
const RETRY_TIMEOUT_MS = 30000

// 只重試 GET / HEAD。POST、PUT、DELETE 逾時後無法確定伺服器處理了沒有：
// Render 冷啟動時請求會先被 proxy 排隊，我們這端中斷了，執行個體醒來後
// 仍可能照常寫入。這時重試就會送出第二則一模一樣的訊息——比原本的問題更糟。
// 要讓寫入請求也能安全重試，得先在後端做 idempotency key。
const RETRYABLE_METHODS = new Set(['GET', 'HEAD'])

async function fetchWithTimeout(url: string, init: RequestInit, timeoutMs: number): Promise<Response> {
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), timeoutMs)
  try {
    // signal 放在 init 之後，確保逾時一定生效、不會被呼叫端蓋掉
    return await fetch(url, { ...init, signal: controller.signal })
  } finally {
    clearTimeout(timer)
  }
}

// 唯一的傳輸層：負責 token、逾時、重試、401 處理、錯誤翻譯。
// 各 feature 的 api.ts 只呼叫這個 request()，不自己拼 fetch。
export async function request<T>(path: string, options?: RequestInit): Promise<ApiResponse<T>> {
  let response: Response
  const token = getAuthToken()
  const authHeader: Record<string, string> = token ? { Authorization: `Bearer ${token}` } : {}
  const url = `${API_BASE_URL}${path}`
  const init: RequestInit = {
    ...options,
    headers: { 'Content-Type': 'application/json', ...authHeader, ...(options?.headers ?? {}) },
  }
  const canRetry = RETRYABLE_METHODS.has((options?.method ?? 'GET').toUpperCase())
  try {
    try {
      response = await fetchWithTimeout(url, init, FIRST_TIMEOUT_MS)
    } catch (first) {
      if (!canRetry) {
        throw first
      }
      // 第一次失敗通常也已經把休眠的服務叫醒了，重試多半就會過
      response = await fetchWithTimeout(url, init, RETRY_TIMEOUT_MS)
    }
  } catch (err) {
    if (err instanceof DOMException && err.name === 'AbortError') {
      throw new Error('連線逾時，請確認後端服務是否已啟動。')
    }
    throw new Error('目前無法連線到伺服器，請確認後端是否已啟動。')
  }

  if (response.status === 401) {
    clearAuthToken()
    rememberClear(STORAGE_KEYS.user)
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
