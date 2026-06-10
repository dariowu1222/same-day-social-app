import type { CapacitorConfig } from '@capacitor/cli'

// ─── 開發模式 ────────────────────────────────────────────
// 讓 app 直接連 Vite dev server，改完程式碼不用重新 build
// 使用前：先啟動 `npm run dev`，再執行 `npx cap sync` + Android Studio Run
//
// ─── 上架模式 ────────────────────────────────────────────
// 把下方 server.url 整行刪除（或加 //），執行：
//   npm run build  →  npx cap sync  →  Android Studio Build APK/AAB

const config: CapacitorConfig = {
  appId: 'com.tongpin.today',
  appName: '同頻 Today',
  webDir: 'dist',
  server: {
    androidScheme: 'https',
    url: 'http://10.0.2.2:5173',  // 開發用 live reload，上架前改回來
    cleartext: true,
  },
  plugins: {
    StatusBar: {
      overlaysWebView: true,  // web 內容延伸到 status bar 後方，配合 safe-area-inset-top
      style: 'LIGHT',         // 預設亮色（深色圖示），切換時由 App.tsx 動態設定
      backgroundColor: '#00000000',  // 透明背景
    },
  },
}

export default config
