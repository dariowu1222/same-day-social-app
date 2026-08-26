import { useState } from 'react'
import { login, getDashboard, clearToken, type AuthUser } from '../api'

type Props = { onLogin: (user: AuthUser) => void }

export default function LoginPage({ onLogin }: Props) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setError('')
    try {
      const user = await login(email.trim(), password)
      // 登入成功不等於有後台權限：立刻打一支 admin 端點確認，
      // 否則使用者會進到全是錯誤訊息的空後台，搞不清楚是壞掉還是沒權限。
      await getDashboard()
      onLogin(user)
    } catch (err) {
      clearToken()
      setError(err instanceof Error ? err.message : '登入失敗，請稍後再試。')
      setBusy(false)
    }
  }

  return (
    <div className="login-wrap">
      <form className="login-card" onSubmit={submit}>
        <h1 className="login-title">同頻 Today 後台</h1>
        <p className="login-sub">請使用具管理權限的帳號登入</p>

        {error && <div className="msg msg-error">{error}</div>}

        <label className="login-field">
          <span>電子郵件</span>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="username"
            required
          />
        </label>

        <label className="login-field">
          <span>密碼</span>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />
        </label>

        <button className="btn btn-primary" style={{ width: '100%', marginTop: 6 }} disabled={busy}>
          {busy ? '登入中…' : '登入'}
        </button>
      </form>
    </div>
  )
}
