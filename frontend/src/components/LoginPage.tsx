import { useState } from 'react'
import type { ReactNode } from 'react'

type AuthMode = 'login' | 'forgot' | 'register'
type ForgotStep = 'account' | 'verify' | 'reset'

type Props = {
  nickname: string
  isLoading: boolean
  onNicknameChange: (value: string) => void
  onLogin: (nicknameOverride?: string) => void
}

export default function LoginPage({ nickname, isLoading, onNicknameChange, onLogin }: Props) {
  const [mode, setMode] = useState<AuthMode>('login')
  const [forgotStep, setForgotStep] = useState<ForgotStep>('account')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [resetCode, setResetCode] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [registerName, setRegisterName] = useState('')
  const [registerUsername, setRegisterUsername] = useState('')
  const [birthYear, setBirthYear] = useState('')
  const [gender, setGender] = useState('PRIVATE')
  const [agreed, setAgreed] = useState(false)

  function openForgot() {
    setForgotStep('account')
    setMode('forgot')
  }

  if (mode === 'forgot') {
    return (
      <AuthFrame variant="warm" onBack={() => setMode('login')}>
        <LogoBlock compact />
        <section className="auth-title-block">
          <h1>忘記密碼</h1>
          <p>{getForgotDescription(forgotStep)}</p>
        </section>

        <div className="forgot-steps" aria-label="忘記密碼流程">
          <span className={forgotStep === 'account' ? 'active' : ''}>1</span>
          <span className={forgotStep === 'verify' ? 'active' : ''}>2</span>
          <span className={forgotStep === 'reset' ? 'active' : ''}>3</span>
        </div>

        <form className="auth-card forgot-card" onSubmit={(event) => event.preventDefault()}>
          {forgotStep === 'account' && (
            <>
              <AuthField icon="✉" value={nickname} onChange={onNicknameChange} placeholder="使用者名稱" />
              <button className="primary-login-button" type="button" onClick={() => setForgotStep('verify')}>
                發送驗證碼
              </button>
            </>
          )}

          {forgotStep === 'verify' && (
            <>
              <AuthField icon="♢" value={resetCode} onChange={setResetCode} placeholder="驗證碼" />
              <div className="forgot-action-row">
                <button className="resend-button" type="button">
                  重新發送
                </button>
                <button className="primary-login-button compact-button" type="button" onClick={() => setForgotStep('reset')}>
                  下一步
                </button>
              </div>
            </>
          )}

          {forgotStep === 'reset' && (
            <>
              <AuthField icon="▣" value={newPassword} onChange={setNewPassword} placeholder="新密碼" type="password" action="◠" />
              <AuthField icon="▣" value={confirmPassword} onChange={setConfirmPassword} placeholder="確認新密碼" type="password" action="◠" />
              <button className="primary-login-button" type="button" onClick={() => setMode('login')}>
                重設密碼
              </button>
            </>
          )}
        </form>

        <p className="auth-switch">
          想起密碼了？
          <button type="button" onClick={() => setMode('login')}>
            返回登入
          </button>
        </p>
      </AuthFrame>
    )
  }

  if (mode === 'register') {
    return (
      <AuthFrame variant="clean" onBack={() => setMode('login')}>
        <LogoBlock compact />
        <section className="register-heading">
          <h1>建立帳號</h1>
          <p>加入同頻 Today，開啟你的共鳴旅程</p>
        </section>

        <form
          className="register-form"
          onSubmit={(event) => {
            event.preventDefault()
            onLogin(registerName.trim() || registerUsername.trim() || '新朋友')
          }}
        >
          <FloatingField label="暱稱">
            <AuthField
              icon="♙"
              value={registerName}
              onChange={setRegisterName}
              placeholder="請輸入你的暱稱"
              suffix={`${registerName.length}/20`}
            />
          </FloatingField>
          <FloatingField label="使用者名稱">
            <AuthField icon="✉" value={registerUsername} onChange={setRegisterUsername} placeholder="請輸入使用者名稱" />
          </FloatingField>
          <FloatingField label="密碼">
            <AuthField icon="▣" value={password} onChange={setPassword} placeholder="請設定 8-16 位的密碼" type="password" action="⊙" />
          </FloatingField>
          <FloatingField label="確認密碼">
            <AuthField icon="▣" value={confirmPassword} onChange={setConfirmPassword} placeholder="請再次輸入密碼" type="password" action="⊙" />
          </FloatingField>
          <FloatingField label="出生年次">
            <AuthField icon="□" value={birthYear} onChange={setBirthYear} placeholder="請選擇你的出生年次" action="⌄" />
          </FloatingField>

          <div className="gender-field">
            <span>性別（選填）</span>
            <div className="gender-options">
              <button type="button" className={gender === 'MALE' ? 'selected' : ''} onClick={() => setGender('MALE')}>
                <span className="gender-icon" aria-hidden="true">◎</span>
                男生
              </button>
              <button type="button" className={gender === 'FEMALE' ? 'selected' : ''} onClick={() => setGender('FEMALE')}>
                <span className="gender-icon" aria-hidden="true">◎</span>
                女生
              </button>
              <button type="button" className={gender === 'PRIVATE' ? 'selected' : ''} onClick={() => setGender('PRIVATE')}>
                <span className="gender-icon" aria-hidden="true">☺</span>
                不想透露
              </button>
            </div>
          </div>

          <label className="terms-row">
            <input type="checkbox" checked={agreed} onChange={(event) => setAgreed(event.target.checked)} />
            <span>
              我已閱讀並同意 <a>《服務條款》</a> 與 <a>《隱私權政策》</a>
            </span>
          </label>

          <button className="register-submit" type="submit" disabled={isLoading || !agreed || registerName.trim().length === 0}>
            建立帳號
          </button>
        </form>

        <div className="or-divider">
          <span></span>
          <em>或</em>
          <span></span>
        </div>

        <button className="google-button" type="button">
          <b>G</b>
          使用 Google 帳號繼續
        </button>

        <p className="auth-switch">
          已經有帳號了？
          <button type="button" onClick={() => setMode('login')}>
            立即登入
          </button>
        </p>
      </AuthFrame>
    )
  }

  return (
    <AuthFrame variant="warm">
      <LogoBlock />
      <h1 className="login-main-title">今天，也找個懂你的人</h1>
      <p className="login-subtitle">從日常共鳴開始，慢慢認識彼此</p>

      <form className="login-form" onSubmit={(event) => { event.preventDefault(); onLogin() }}>
        <AuthField icon="✉" value={nickname} onChange={onNicknameChange} placeholder="使用者名稱" />
        <AuthField
          icon="▣"
          value={password}
          onChange={setPassword}
          placeholder="密碼"
          type={showPassword ? 'text' : 'password'}
          action="◠"
          onAction={() => setShowPassword(!showPassword)}
        />
        <button className="primary-login-button" type="submit" disabled={isLoading || nickname.trim().length === 0}>
          {isLoading ? '登入中' : '登入'}
        </button>
      </form>

      <button className="forgot-button" type="button" onClick={openForgot}>
        忘記密碼？
      </button>

      <button className="outline-login-button" type="button" onClick={() => setMode('register')}>
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
    </AuthFrame>
  )
}

function getForgotDescription(step: ForgotStep) {
  if (step === 'account') {
    return '輸入你的使用者名稱，我們會協助你重設密碼'
  }

  if (step === 'verify') {
    return '輸入收到的驗證碼，確認這是你的帳號'
  }

  return '設定新密碼後，就可以回到登入頁'
}

function AuthFrame({ children, variant, onBack }: { children: ReactNode; variant: 'warm' | 'clean'; onBack?: () => void }) {
  return (
    <main className={`login-screen ${variant === 'clean' ? 'register-screen' : ''}`}>
      <div className="login-status" aria-hidden="true">
        <strong>9:41</strong>
        <span>▮▮▮ ))) ▭</span>
      </div>
      {onBack && (
        <button className="back-button" type="button" onClick={onBack} aria-label="返回">
          ←
        </button>
      )}
      <section className="login-content">
        {variant === 'warm' && (
          <>
            <div className="login-decoration chat-left">♡</div>
            <div className="login-decoration chat-right">•••</div>
            <div className="login-decoration sparkle-one">✦</div>
            <div className="login-decoration sparkle-two">✧</div>
          </>
        )}
        {children}
      </section>
    </main>
  )
}

function LogoBlock({ compact = false }: { compact?: boolean }) {
  return (
    <>
      <div className={`login-logo ${compact ? 'compact' : ''}`} aria-hidden="true">
        <span className="person-one"></span>
        <span className="person-two"></span>
        <span className="logo-heart"></span>
      </div>
      <p className="login-brand">同頻 Today</p>
    </>
  )
}

function AuthField({
  icon,
  value,
  onChange,
  placeholder,
  type = 'text',
  action,
  onAction,
  suffix,
}: {
  icon: string
  value: string
  onChange: (value: string) => void
  placeholder: string
  type?: string
  action?: string
  onAction?: () => void
  suffix?: string
}) {
  return (
    <label className="login-field">
      <span className="field-icon" aria-hidden="true">{icon}</span>
      <input value={value} onChange={(event) => onChange(event.target.value)} placeholder={placeholder} type={type} />
      {suffix && <span className="field-suffix">{suffix}</span>}
      {action && (
        <button className="field-action" type="button" onClick={onAction} aria-label="欄位動作">
          {action}
        </button>
      )}
    </label>
  )
}

function FloatingField({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="floating-field">
      <span>{label}</span>
      {children}
    </div>
  )
}
