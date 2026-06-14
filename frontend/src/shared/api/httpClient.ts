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

// 唯一的傳輸層：負責 token、逾時、401 處理、錯誤翻譯。
// 各 feature 的 api.ts 只呼叫這個 request()，不自己拼 fetch。
export async function request<T>(path: string, options?: RequestInit): Promise<ApiResponse<T>> {
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
