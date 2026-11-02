import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

import { readFileSync, existsSync } from 'node:fs'

// 版本號統一由 package.json 與 Android 的 build.gradle 取得，建置時注入。
// 先前設定頁寫死 '0.1.0'，與實際安裝的版本對不上，測試者無從判斷手上是哪一版。
const pkgVersion = JSON.parse(readFileSync('./package.json', 'utf-8')).version as string
const versionCode =
  readFileSync('./android/app/build.gradle', 'utf-8').match(/versionCode\s+(\d+)/)?.[1] ?? '0'

// 推播要有 Firebase 設定才動得了，沒有就整條停用——跟後端同一個原則。
//
// 為什麼不能只靠 try/catch：Capacitor 的 push 外掛只要裝了就會被編進 app，
// 而它 register() 的第一行是 FirebaseMessaging.getInstance()，沒有任何保護。
// 缺 google-services.json 時那行丟 IllegalStateException，直接把 app 帶走。
// 1.0.5 就是這樣——已登入的裝置一過開場畫面，App.tsx 的 syncPushRegistration
// 就會踩到它。全新安裝因為沒登入、也沒有從舊版沿用過來的通知權限，所以測不出來。
const pushEnabled = existsSync('./android/app/google-services.json')
if (!pushEnabled) {
  console.warn('[build] 找不到 android/app/google-services.json，推播功能在這個包裡停用')
}

export default defineConfig({
  define: {
    __APP_VERSION__: JSON.stringify(pkgVersion),
    __APP_BUILD__: JSON.stringify(versionCode),
    __PUSH_ENABLED__: JSON.stringify(pushEnabled),
  },
  plugins: [react()],
  server: {
    host: '0.0.0.0',  // 讓區域網路與模擬器都能連到 dev server
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        ws: true,
      },
    },
  },
})
