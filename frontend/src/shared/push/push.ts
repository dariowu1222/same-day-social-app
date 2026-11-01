import { Capacitor } from '@capacitor/core'
import { PushNotifications } from '@capacitor/push-notifications'
import { registerPushToken, unregisterPushToken } from './api'

/**
 * FCM 推播註冊。
 *
 * 權限請求的時機很關鍵：Android 13 起要 runtime 權限，而且**連續拒絕兩次之後
 * 系統就永遠不再跳視窗**。所以 App 啟動時絕不主動問——那時使用者還不知道
 * 這個 App 會通知什麼，按掉的機率很高，而且按掉就救不回來。
 * 改成第一次真的進到某個對話裡才問，那時「想知道對方回了沒」的理由自明。
 *
 * 拒絕過的人可以在設定 → 通知設定裡手動補開。
 */

const TOKEN_KEY = 'push-fcm-token'
const ASKED_KEY = 'push-permission-asked'

export type PushPermission = 'granted' | 'denied' | 'prompt' | 'unsupported'

function supported() {
  return Capacitor.isNativePlatform()
}

function readStored(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function writeStored(key: string, value: string | null) {
  try {
    if (value === null) localStorage.removeItem(key)
    else localStorage.setItem(key, value)
  } catch {
    // 私密視窗或關掉站台資料時會丟例外，忽略即可
  }
}

/** 等外掛把 token 送回來。register() 本身只是觸發，token 走事件回來。 */
function awaitToken(timeoutMs = 15000): Promise<string | null> {
  return new Promise((resolve) => {
    let settled = false
    const finish = (value: string | null) => {
      if (settled) return
      settled = true
      resolve(value)
    }

    void PushNotifications.addListener('registration', (token) => finish(token.value))
    void PushNotifications.addListener('registrationError', () => finish(null))
    setTimeout(() => finish(null), timeoutMs)

    void PushNotifications.register().catch(() => finish(null))
  })
}

async function sendToken(token: string) {
  writeStored(TOKEN_KEY, token)
  await registerPushToken(token, Capacitor.getPlatform())
}

export async function getPushPermission(): Promise<PushPermission> {
  if (!supported()) return 'unsupported'
  try {
    const status = await PushNotifications.checkPermissions()
    return status.receive === 'granted' ? 'granted'
      : status.receive === 'denied' ? 'denied'
      : 'prompt'
  } catch {
    return 'unsupported'
  }
}

/**
 * 啟動時呼叫。只有「已經授權」才會靜默重新註冊——FCM token 會輪替，
 * 不定期重送的話伺服器那份會過期，推播就靜靜地不會到。
 * 沒授權時什麼都不做，不跳任何視窗。
 */
export async function syncPushRegistration() {
  if (!supported()) return
  if (await getPushPermission() !== 'granted') return

  const token = await awaitToken()
  if (token) await sendToken(token)
}

/**
 * 跳系統權限視窗並註冊。回傳是否成功拿到授權。
 *
 * 只在「第一次進到某個對話」時由 UI 呼叫一次；已經問過就不再自動問，
 * 免得使用者連按兩次拒絕之後，系統永久封鎖這個視窗。
 */
export async function enablePush(options?: { force?: boolean }): Promise<boolean> {
  if (!supported()) return false

  const current = await getPushPermission()
  if (current === 'granted') {
    const token = await awaitToken()
    if (token) await sendToken(token)
    return true
  }

  // 系統已經封鎖（拒絕兩次）時，request 會直接回 denied 而不跳視窗。
  // 這時要引導使用者去系統設定，不是在這裡重試。
  if (current === 'denied' && !options?.force) return false
  if (!options?.force && readStored(ASKED_KEY)) return false

  writeStored(ASKED_KEY, '1')

  try {
    const result = await PushNotifications.requestPermissions()
    if (result.receive !== 'granted') return false
  } catch {
    return false
  }

  const token = await awaitToken()
  if (token) await sendToken(token)
  return true
}

/**
 * 登出時解除綁定，免得下一個登入這台裝置的人收到前一個人的推播。
 *
 * 絕不要讓登出等這支：httpClient 對寫入請求的逾時是 75 秒，Render 休眠時
 * 按登出會整個卡住。所以本機先清掉（這步一定成功），伺服器那邊盡力而為。
 */
export function detachPush() {
  const token = readStored(TOKEN_KEY)
  writeStored(TOKEN_KEY, null)
  if (!token) return
  void unregisterPushToken(token).catch(() => {})
}

/**
 * 使用者點了推播通知。帶 roomId 的話直接開那間房（只有一個對話有未讀時才會帶）。
 * 回傳取消監聽的函式。
 */
export function onPushOpened(handler: (roomId: string | null) => void): () => void {
  if (!supported()) return () => {}

  const pending = PushNotifications.addListener('pushNotificationActionPerformed', (action) => {
    const roomId = action.notification.data?.roomId
    handler(typeof roomId === 'string' && roomId ? roomId : null)
  })

  return () => { void pending.then((listener) => listener.remove()).catch(() => {}) }
}
