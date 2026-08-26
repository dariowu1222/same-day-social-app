import { useState } from 'react'
import { BrowserRouter, NavLink, Navigate, Route, Routes } from 'react-router-dom'
import { clearToken, getToken, type AuthUser } from './api'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import UsersPage from './pages/UsersPage'
import UserDetailPage from './pages/UserDetailPage'
import ReportsPage from './pages/ReportsPage'

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

  function handleLogout() {
    clearToken()
    try {
      sessionStorage.removeItem(USER_KEY)
    } catch { /* ignore */ }
    setUser(null)
  }

  if (!user) {
    return <LoginPage onLogin={handleLogin} />
  }

  return (
    <BrowserRouter>
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
          </nav>
          <div className="sidebar-foot">
            <div className="sidebar-user">{user.nickname}<br />{user.email}</div>
            <button className="btn btn-sm" onClick={handleLogout}>登出</button>
          </div>
        </aside>

        <main className="main">
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/users" element={<UsersPage />} />
            <Route path="/users/:userId" element={<UserDetailPage />} />
            <Route path="/reports" element={<ReportsPage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  )
}
