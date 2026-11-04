import { useEffect, useState } from 'react'
import { BrowserRouter, NavLink, Navigate, Route, Routes } from 'react-router-dom'
import { clearToken, getToken, logout, type AuthUser } from './api'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import UsersPage from './pages/UsersPage'
import UserDetailPage from './pages/UserDetailPage'
import ReportsPage from './pages/ReportsPage'
import AiUsagePage from './pages/AiUsagePage'

const USER_KEY = 'admin_user'

function readStoredUser(): AuthUser | null {
  try {
    const raw = sessionStorage.getItem(USER_KEY)
    return raw ? (JSON.parse(raw) as AuthUser) : null
  } catch {
    return null
  }
}

export default function App() {
  // token 與使用者都放 sessionStorage：關掉分頁即登出（見 api.ts 的說明）
  const [user, setUser] = useState<AuthUser | null>(() => (getToken() ? readStoredUser() : null))

  function handleLogin(next: AuthUser) {
    try {
      sessionStorage.setItem(USER_KEY, JSON.stringify(next))
    } catch { /* ignore */ }
    setUser(next)
  }

  const [loggingOut, setLoggingOut] = useState(false)

  async function handleLogout() {
    if (loggingOut) return
    setLoggingOut(true)
    // 要等後端真的把 session 撤銷再切畫面。不等的話，使用者按完就關分頁，
    // 請求可能根本沒送出去，token 會一直活到過期。
    await logout()
    try {
      sessionStorage.removeItem(USER_KEY)
    } catch { /* ignore */ }
    setLoggingOut(false)
    setUser(null)
  }

  // session 過期（閒置超時、被撤銷、或絕對上限到了）時，api.ts 會清掉 token
  // 並丟 401。這裡把畫面退回登入頁，不要讓使用者對著一堆錯誤訊息猜發生什麼事。
  function handleExpired() {
    clearToken()
    try {
      sessionStorage.removeItem(USER_KEY)
    } catch { /* ignore */ }
    setUser(null)
  }

  useEffect(() => {
    window.addEventListener('admin-session-expired', handleExpired)
    return () => window.removeEventListener('admin-session-expired', handleExpired)
  })

  if (!user) {
    return <LoginPage onLogin={handleLogin} />
  }

  return (
    // basename：後台部署在後端的 /admin 子路徑底下，少了它所有連結都會指到根目錄
    <BrowserRouter basename={import.meta.env.BASE_URL}>
      <div className="shell">
        <aside className="sidebar">
          <div className="sidebar-brand">同頻 Today 後台</div>
          <nav className="sidebar-nav">
            <NavLink to="/" end className={({ isActive }) => `sidebar-link${isActive ? ' active' : ''}`}>
              總覽
            </NavLink>
            <NavLink to="/users" className={({ isActive }) => `sidebar-link${isActive ? ' active' : ''}`}>
              會員管理
            </NavLink>
            <NavLink to="/reports" className={({ isActive }) => `sidebar-link${isActive ? ' active' : ''}`}>
              檢舉處理
            </NavLink>
            <NavLink to="/ai-usage" className={({ isActive }) => `sidebar-link${isActive ? ' active' : ''}`}>
              AI 用量
            </NavLink>
          </nav>
          <div className="sidebar-foot">
            <div className="sidebar-user">{user.nickname}<br />{user.email}</div>
            <button className="btn btn-sm" onClick={() => void handleLogout()} disabled={loggingOut}>
              {loggingOut ? '登出中…' : '登出'}
            </button>
          </div>
        </aside>

        <main className="main">
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/users" element={<UsersPage />} />
            <Route path="/users/:userId" element={<UserDetailPage />} />
            <Route path="/reports" element={<ReportsPage />} />
            <Route path="/ai-usage" element={<AiUsagePage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  )
}
