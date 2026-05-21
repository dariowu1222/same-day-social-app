import { useState } from 'react'

type Props = {
  nickname: string
  isLoading: boolean
  onNicknameChange: (value: string) => void
  onLogin: (nicknameOverride?: string) => void
}

export default function LoginPage({ nickname, isLoading, onNicknameChange, onLogin }: Props) {
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)

  return (
    <main className="login-screen">
      <div className="login-status" aria-hidden="true">
        <strong>9:41</strong>
        <span>▮▮▮ ))) ▭</span>
      </div>

      <section className="login-content" aria-labelledby="login-title">
        <div className="login-decoration chat-left">♡</div>
        <div className="login-decoration chat-right">•••</div>
        <div className="login-decoration sparkle-one">✦</div>
        <div className="login-decoration sparkle-two">✧</div>

        <div className="login-logo" aria-hidden="true">
          <span className="person-one"></span>
          <span className="person-two"></span>
          <span className="logo-heart"></span>
        </div>

        <p className="login-brand">同頻 Today</p>
        <h1 id="login-title">今天，也找個懂你的人</h1>
        <p className="login-subtitle">從日常共鳴開始，慢慢認識彼此</p>

        <form className="login-form" onSubmit={(event) => { event.preventDefault(); onLogin() }}>
          <label className="login-field">
            <span className="field-icon" aria-hidden="true">✉</span>
            <input
              value={nickname}
              onChange={(event) => onNicknameChange(event.target.value)}
              placeholder="Email / 手機號碼"
              autoComplete="username"
            />
          </label>

          <label className="login-field">
            <span className="field-icon" aria-hidden="true">▣</span>
            <input
              type={showPassword ? 'text' : 'password'}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="密碼"
              autoComplete="current-password"
            />
            <button className="field-action" type="button" onClick={() => setShowPassword(!showPassword)} aria-label="切換密碼顯示">
              ◠
            </button>
          </label>

          <button className="primary-login-button" type="submit" disabled={isLoading || nickname.trim().length === 0}>
            {isLoading ? '登入中' : '登入'}
          </button>
        </form>

        <button className="forgot-button" type="button">
          忘記密碼？
        </button>

        <button className="outline-login-button" type="button" onClick={() => onLogin(nickname.trim() || '新朋友')}>
          <span aria-hidden="true">♙</span>
          建立帳號
        </button>

        <div className="guest-divider">
          <span></span>
          <b>♥</b>
          <em>也可以先看看</em>
          <b>♥</b>
          <span></span>
        </div>

        <button className="guest-button" type="button" onClick={() => onLogin('訪客')}>
          <span aria-hidden="true">⌕</span>
          訪客進入
        </button>
      </section>
    </main>
  )
}
