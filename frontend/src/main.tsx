import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { HashRouter } from 'react-router-dom'
import App from './app/App.tsx'
import { ThemeProvider } from './shared/theme/ThemeContext.tsx'
import { AuthProvider } from './features/auth/AuthContext.tsx'
import { ChatRealtimeProvider } from './features/chat/ChatRealtimeContext.tsx'
import './styles/global.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <HashRouter>
      <ThemeProvider>
        <AuthProvider>
          {/* 即時連線要跨分頁存活，否則人不在聊天頁時收不到新訊息、未讀紅點不會動 */}
          <ChatRealtimeProvider>
            <App />
          </ChatRealtimeProvider>
        </AuthProvider>
      </ThemeProvider>
    </HashRouter>
  </StrictMode>,
)
