import { request } from '../api/httpClient'

/** 把這台裝置的 FCM token 綁到目前登入的使用者。token 會輪替，每次啟動都要重送。 */
export async function registerPushToken(token: string, platform: string) {
  return request<boolean>('/api/push/tokens', {
    method: 'POST',
    body: JSON.stringify({ token, platform }),
  })
}

/** 登出時解除綁定。 */
export async function unregisterPushToken(token: string) {
  return request<boolean>('/api/push/tokens/remove', {
    method: 'POST',
    body: JSON.stringify({ token }),
  })
}
