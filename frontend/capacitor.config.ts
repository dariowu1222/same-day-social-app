import type { CapacitorConfig } from '@capacitor/cli'

// ─── 兩種模式 ────────────────────────────────────────────
// 開發模式（預設）：app 直接連 Vite dev server，改完程式碼不用重新 build
//   使用前：先啟動 `npm run dev`，再執行 `npx cap sync` + Android Studio Run
//
// 實機測試 / 上架模式：打包 dist 進 app，不依賴 dev server
//   執行：`npm run cap:device`（= vite build --mode device + CAP_BUILD=device cap sync）
//   API 位址由 frontend/.env.device 的 VITE_API_BASE_URL 決定
//   詳細步驟見 docs/device-build.md

const isDeviceBuild = process.env.CAP_BUILD === 'device'

const config: CapacitorConfig = {
  appId: 'com.tongpin.today',
  appName: '同頻 Today',
  webDir: 'dist',
  server: isDeviceBuild
    ? {
        androidScheme: 'https',
      }
    : {
        androidScheme: 'https',
        url: 'http://10.0.2.2:5173',  // 開發用 live reload（Android 模擬器）
        cleartext: true,
      },
  android: {
    // 實機測試時 app（https://localhost）需要呼叫 http://<電腦IP>:5000
    // 上架正式版（後端有 https）後應改回 false
    allowMixedContent: true,
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
