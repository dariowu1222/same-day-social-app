import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

import { readFileSync } from 'node:fs'

// 版本號統一由 package.json 與 Android 的 build.gradle 取得，建置時注入。
// 先前設定頁寫死 '0.1.0'，與實際安裝的版本對不上，測試者無從判斷手上是哪一版。
const pkgVersion = JSON.parse(readFileSync('./package.json', 'utf-8')).version as string
const versionCode =
  readFileSync('./android/app/build.gradle', 'utf-8').match(/versionCode\s+(\d+)/)?.[1] ?? '0'


export default defineConfig({
  define: {
    __APP_VERSION__: JSON.stringify(pkgVersion),
    __APP_BUILD__: JSON.stringify(versionCode),
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
