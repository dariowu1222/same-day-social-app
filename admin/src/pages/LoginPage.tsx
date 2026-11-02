import { useState } from 'react'
import { loginStart, loginVerify, resendCode, getDashboard, clearToken, type AuthUser } from '../api'

type Props = { onLogin: (user: AuthUser) => void }

/**
 * 兩段式登入。
 *
 * 第一段是 Email + 密碼，和 App 用的是同一組帳號；第二段是寄到信箱的六位數。
 * 第二因素刻意只擺在這裡，不擺在 App 的登入——擋的是「密碼外洩的人想進後台」，
 * 沒有理由讓全站使用者為了這個風險每次登入都多一道手續。
 */
export default function LoginPage({ onLogin }: Props) {
  const [step, setStep] = useState<'password' | 'code'>('password')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [code, setCode] = useState('')
  const [maskedEmail, setMaskedEmail] = useState('')
  const [pendingUser, setPendingUser] = useState<AuthUser | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  async function submitPassword(e: React.FormEvent) {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setError('')
    try {
      const { user, maskedEmail: masked } = await loginStart(email.trim(), password)
      setPendingUser(user)
      setMaskedEmail(masked)
      setStep('code')
      // 密碼留在 state 裡沒有意義，而且多一份在記憶體裡的機會被翻出來
      setPassword('')
    } catch (err) {
      clearToken()
      setError(err instanceof Error ? err.message : '登入失敗，請稍後再試。')
    }
    setBusy(false)
  }

  async function submitCode(e: React.FormEvent) {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setError('')
    try {
      await loginVerify(code.trim())
      // 驗過不等於有後台權限：立刻打一支 admin 端點確認，
      // 否則會進到全是錯誤訊息的空後台，搞不清楚是壞掉還是沒權限。
      await getDashboard()
      onLogin(pendingUser!)
    } catch (err) {
      clearToken()
      setError(err instanceof Error ? err.message : '驗證失敗，請稍後再試。')
      setBusy(false)
    }
  }

  async function resend() {
    if (busy) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const masked = await resendCode()
      setMaskedEmail(masked)
      setNotice('已重新寄出驗證碼。')
    } catch (err) {
      setError(err instanceof Error ? err.message : '重寄失敗，請稍後再試。')
    }
    setBusy(false)
  }

  function backToPassword() {
    setStep('password')
    setCode('')
    setError('')
    setNotice('')
    clearToken()
  }

  if (step === 'code') {
    return (
      <div className="login-wrap">
        <form className="login-card" onSubmit={submitCode}>
          <h1 className="login-title">輸入驗證碼</h1>
          <p className="login-sub">已寄出六位數驗證碼到 {maskedEmail}，5 分鐘內有效。</p>

          {error && <div className="msg msg-error">{error}</div>}
          {notice && <div className="msg">{notice}</div>}

          <label className="login-field">
            <span>驗證碼</span>
            <input
              value={code}
              onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
              inputMode="numeric"
              autoComplete="one-time-code"
              maxLength={6}
              autoFocus
              required
            />
          </label>

          <button className="btn btn-primary" style={{ width: '100%', marginTop: 6 }} disabled={busy || code.length < 6}>
            {busy ? '驗證中…' : '進入後台'}
          </button>

          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 12 }}>
            <button type="button" className="btn-link" onClick={backToPassword} disabled={busy}>
              改用其他帳號
            </button>
            <button type="button" className="btn-link" onClick={resend} disabled={busy}>
              沒收到？重新寄送
            </button>
          </div>
        </form>
      </div>
    )
  }

  return (
    <div className="login-wrap">
      <form className="login-card" onSubmit={submitPassword}>
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
          {busy ? '登入中…' : '下一步'}
        </button>
      </form>
    </div>
  )
}
