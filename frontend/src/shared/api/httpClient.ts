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

/**
 * 帶著錯誤碼的失敗。
 *
 * 先前一律 throw new Error(翻譯後的訊息)，code 在這裡就被丟掉了，所以呼叫端
 * 只能一視同仁顯示「失敗」。有些碼需要的不只是一句話——例如聊天室在使用者
 * 還開著的時候被後台凍結，送出會回 CHAT_ROOM_REPORTED，這時該做的是重抓
 * 房間狀態把輸入列換掉，而不是叫他再試一次。
 *
 * 繼承 Error，所以既有的 catch 與 err.message 都不受影響。
 */
export class ApiError extends Error {
  readonly code?: string
  constructor(message: string, code?: string) {
    super(message)
    this.name = 'ApiError'
    this.code = code
  }
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

/**
 * 把路徑參數逐段編碼的樣板字串：apiPath`/api/chats/${roomId}/read`
 *
 * 為什麼需要：各 api.ts 都是直接把 id 內插進路徑的，而有些 id 來自網址
 * （ChatPage 的 ?room=）。searchParams.get 會把 %2F、%2E%2E、%3F 解碼回來，
 * 所以一個 /chat?room=../../../api/<某端點>%3F 的連結，會讓已登入的受害者
 * 在不知情的情況下對那個端點送出一發帶著自己 Bearer token 的 POST——
 * 瀏覽器會把 .. 正規化掉，? 之後的 /read 被當成 query。
 *
 * 檢舉、封鎖、退出聊天室這些端點都不需要 body，所以真的打得動。
 *
 * 編碼之後 ../ 變成 ..%2F，路徑停在原本那一段，什麼都跳不出去。
 */
export function apiPath(parts: TemplateStringsArray, ...values: unknown[]): string {
  return parts.reduce(
    (acc, part, i) => acc + part + (i < values.length ? encodeURIComponent(String(values[i])) : ''),
    '',
  )
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
    throw new ApiError(toUserMessage(body.code, body.message), body.code)
  }
  return body
}
