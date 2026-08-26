import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// 後台是獨立專案，與使用者端 App 分開部署：
// 後台程式碼不會進到使用者的 bundle，也不對外暴露這些路由的存在。
export default defineConfig({
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
})
