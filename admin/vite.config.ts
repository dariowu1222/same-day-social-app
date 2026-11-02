import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// 後台是獨立專案，與使用者端 App 分開部署：
// 後台程式碼不會進到使用者的 bundle，也不對外暴露這些路由的存在。
export default defineConfig(({ command }) => ({
  // 正式環境由後端託管在 /admin 底下，資產路徑要跟著走。
  // 本機 dev 維持根路徑，不然每次都得記得在網址後面加 /admin/。
  // App.tsx 的 BrowserRouter 讀 import.meta.env.BASE_URL，兩邊自動一致。
  base: command === 'build' ? '/admin/' : '/',
  plugins: [react()],
  server: {
    port: 5174,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
}))
