import { Capacitor } from '@capacitor/core'
import { SocialLogin } from '@capgo/capacitor-social-login'

// 社群登入設定（皆非機密，由 .env 提供；未設定的供應商按鈕會顯示「尚未設定」）。
// Google Web Client ID：Google Cloud Console 的 OAuth Web 用戶端 → VITE_GOOGLE_WEB_CLIENT_ID
// Facebook App ID / Client Token：Meta for Developers 的 App → VITE_FACEBOOK_APP_ID / VITE_FACEBOOK_CLIENT_TOKEN
export const GOOGLE_WEB_CLIENT_ID = import.meta.env.VITE_GOOGLE_WEB_CLIENT_ID as string | undefined
export const FACEBOOK_APP_ID = import.meta.env.VITE_FACEBOOK_APP_ID as string | undefined
const FACEBOOK_CLIENT_TOKEN = import.meta.env.VITE_FACEBOOK_CLIENT_TOKEN as string | undefined

let initialized = false

// App 啟動時與登入前都會呼叫一次：
// - 主視窗：初始化外掛供之後登入用。
// - 網頁版 popup 視窗：實例化 web 實作，其建構子會自動把 OAuth 結果回傳給開啟它的主視窗。
export async function ensureSocialInit() {
  if (initialized) return
  const isWeb = Capacitor.getPlatform() === 'web'
  const config: {
    google?: { webClientId: string; redirectUrl?: string }
    facebook?: { appId: string; clientToken: string }
  } = {}
  if (GOOGLE_WEB_CLIENT_ID) {
    config.google = {
      webClientId: GOOGLE_WEB_CLIENT_ID,
      // 網頁版 popup 流程的 redirect_uri 固定為網站根路徑（須在 Web 用戶端登記同一個值）；
      // 原生 Android/iOS 不需要、也不傳，沿用原本行為。
      ...(isWeb ? { redirectUrl: `${window.location.origin}/` } : {}),
    }
  }
  if (FACEBOOK_APP_ID) {
    config.facebook = { appId: FACEBOOK_APP_ID, clientToken: FACEBOOK_CLIENT_TOKEN ?? '' }
  }
  if (!config.google && !config.facebook) return
  await SocialLogin.initialize(config)
  initialized = true
}
