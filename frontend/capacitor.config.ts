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
    // url: 'http://10.0.2.2:5173',  // ← 開發時取消這行的 //
    // cleartext: true,               // ← 開發時取消這行的 //
  },
}

export default config
