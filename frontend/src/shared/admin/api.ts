import { request } from '../api/httpClient'

/**
 * 這個帳號是不是管理者。
 *
 * 後端對非管理者回 404，所以這裡把「404」當成正常答案而不是錯誤——
 * 一般使用者本來就不該知道有這支端點。回 false 時 App 不會顯示任何後台相關的東西。
 *
 * 注意：這只決定「看不看得到入口」。真正的權限在後端，而且進後台還要再過
 * 一次信箱驗證碼。藏起來的入口不是防護。
 */
export async function checkIsAdmin(): Promise<boolean> {
  try {
    const res = await request<{ isAdmin: boolean }>('/api/admin/me')
    return !!res.data?.isAdmin
  } catch {
    return false
  }
}
