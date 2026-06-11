import { useState } from 'react'
import type { ReactNode } from 'react'
import { CalendarDays, Eye, EyeOff, LockKeyhole, Mail, ShieldCheck, UserRound } from 'lucide-react'
import {
  confirmRegistration,
  confirmPasswordReset,
  demoLogin,
  loginAccount,
  registerAccount,
  requestPasswordReset,
  verifyPasswordReset,
} from '../api/client'
import type { DemoUser } from '../App'

type AuthMode = 'login' | 'forgot' | 'register'
type ForgotStep = 'account' | 'verify' | 'reset'
type RegisterStep = 'form' | 'verify'
type LegalDoc = 'terms' | 'privacy'

type Props = {
  onAuthenticated: (user: DemoUser, remember: boolean) => void
}

function isAdultBirthYear(birthYear: string) {
  const normalized = birthYear.replace(/\D/g, '').slice(0, 4)
  if (normalized.length < 4) return false

  const year = Number(normalized)
  const currentYear = new Date().getFullYear()
  return year >= 1940 && year <= currentYear - 18
}

export default function LoginPage({ onAuthenticated }: Props) {
  const [mode, setMode] = useState<AuthMode>('login')
  const [forgotStep, setForgotStep] = useState<ForgotStep>('account')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [showRegisterPassword, setShowRegisterPassword] = useState(false)
  const [showNewPassword, setShowNewPassword] = useState(false)
  const [resetCode, setResetCode] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [registerName, setRegisterName] = useState('')
  const [registerEmail, setRegisterEmail] = useState('')
  const [registerCode, setRegisterCode] = useState('')
  const [registerStep, setRegisterStep] = useState<RegisterStep>('form')
  const [birthYear, setBirthYear] = useState('')
  const [gender, setGender] = useState('PRIVATE')
  const [agreed, setAgreed] = useState(false)
  const [legalDoc, setLegalDoc] = useState<LegalDoc | null>(null)
  const [authMessage, setAuthMessage] = useState('')
  const [authError, setAuthError] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [demoNickname, setDemoNickname] = useState('')
  const [rememberMe, setRememberMe] = useState(true)
  const [loginEmailError, setLoginEmailError] = useState('')
  const [registerEmailError, setRegisterEmailError] = useState('')
  const [confirmPasswordError, setConfirmPasswordError] = useState('')

  function resetStatus() {
    setAuthMessage('')
    setAuthError('')
  }

  function isValidEmail(value: string) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
  }

  function handleEmailChange(value: string) {
    setEmail(value)
    if (loginEmailError) setLoginEmailError('')
  }

  function handleRegisterEmailChange(value: string) {
    setRegisterEmail(value)
    if (registerEmailError) setRegisterEmailError('')
  }

  function handleConfirmPasswordChange(value: string) {
    setConfirmPassword(value)
    if (confirmPasswordError) setConfirmPasswordError('')
  }

  function openForgot() {
    resetStatus()
    setForgotStep('account')
    setMode('forgot')
  }

  async function runAuthAction(action: () => Promise<void>) {
    setSubmitting(true)
    resetStatus()
    try {
      await action()
    } catch (error) {
      setAuthError(error instanceof Error ? error.message : '操作失敗，請稍後再試。')
    } finally {
      setSubmitting(false)
    }
  }

  async function submitLogin() {
    await runAuthAction(async () => {
      const user = await loginAccount({ email, password }, rememberMe)
      onAuthenticated(user, rememberMe)
    })
  }

  async function submitDemoLogin() {
    await runAuthAction(async () => {
      const user = await demoLogin(demoNickname.trim() || '同頻使用者')
      onAuthenticated(user, false)
    })
  }

  async function submitRegister() {
    await runAuthAction(async () => {
      if (!agreed) {
        throw new Error('請先閱讀並同意服務條款與隱私權政策。')
      }

      if (!isAdultBirthYear(birthYear)) {
        throw new Error('未滿 18 歲暫時不能使用同頻 Today。')
      }

      const result = await registerAccount({
        nickname: registerName,
        email: registerEmail,
        password,
        confirmPassword,
        birthYear,
        gender,
        termsAccepted: agreed,
      })
      setAuthMessage(`驗證碼已寄到 ${result.data.email}，${result.data.expiresInMinutes} 分鐘內有效。`)
      setRegisterStep('verify')
    })
  }

  async function submitRegistrationCode() {
    await runAuthAction(async () => {
      const user = await confirmRegistration({ email: registerEmail, code: registerCode }, true)
      onAuthenticated(user, true)
    })
  }

  async function submitResetRequest() {
    await runAuthAction(async () => {
      const result = await requestPasswordReset(email)
      setAuthMessage(`驗證碼已寄到 ${result.data.email}，${result.data.expiresInMinutes} 分鐘內有效。`)
      setForgotStep('verify')
    })
  }

  async function submitVerifyCode() {
    await runAuthAction(async () => {
      await verifyPasswordReset({ email, code: resetCode })
      setAuthMessage('驗證成功，請設定新密碼。')
      setForgotStep('reset')
    })
  }

  async function submitResetPassword() {
    await runAuthAction(async () => {
      const user = await confirmPasswordReset({
        email,
        code: resetCode,
        newPassword,
        confirmPassword,
      })
      onAuthenticated(user, true)
    })
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
          <span className={forgotStep === 'account' ? 'active' : 'done'}>1</span>
          <span className={`step-connector ${forgotStep !== 'account' ? 'done' : ''}`} />
          <span className={forgotStep === 'verify' ? 'active' : forgotStep === 'reset' ? 'done' : ''}>2</span>
          <span className={`step-connector ${forgotStep === 'reset' ? 'done' : ''}`} />
          <span className={forgotStep === 'reset' ? 'active' : ''}>3</span>
        </div>

        <form className="auth-card forgot-card" onSubmit={(event) => event.preventDefault()}>
          {forgotStep === 'account' && (
            <>
              <AuthField
              icon="mail"
              value={email}
              onChange={handleEmailChange}
              placeholder="Email"
              inputMode="email"
              autoComplete="email"
              error={loginEmailError}
              onBlur={() => { if (email && !isValidEmail(email)) setLoginEmailError('請輸入有效的 Email 格式') }}
            />
              <button className="primary-login-button" type="button" disabled={submitting || !email.trim()} onClick={submitResetRequest}>
                {submitting && <span className="btn-spinner" aria-hidden="true" />}發送驗證碼
              </button>
            </>
          )}

          {forgotStep === 'verify' && (
            <>
              <AuthField icon="code" value={resetCode} onChange={setResetCode} placeholder="6 位數驗證碼" inputMode="numeric" autoComplete="one-time-code" />
              <div className="forgot-action-row">
                <button className="resend-button" type="button" disabled={submitting} onClick={submitResetRequest}>
                  重新發送
                </button>
                <button className="primary-login-button compact-button" type="button" disabled={submitting || resetCode.length < 6} onClick={submitVerifyCode}>
                  {submitting && <span className="btn-spinner" aria-hidden="true" />}下一步
                </button>
              </div>
            </>
          )}

          {forgotStep === 'reset' && (
            <>
              <AuthField
                icon="lock"
                value={newPassword}
                onChange={setNewPassword}
                placeholder="新密碼"
                type={showNewPassword ? 'text' : 'password'}
                autoComplete="new-password"
                isPasswordToggle
                isPasswordVisible={showNewPassword}
                onAction={() => setShowNewPassword(!showNewPassword)}
              />
              <AuthField
                icon="lock"
                value={confirmPassword}
                onChange={setConfirmPassword}
                placeholder="確認新密碼"
                type={showNewPassword ? 'text' : 'password'}
                autoComplete="new-password"
                isPasswordToggle
                isPasswordVisible={showNewPassword}
                onAction={() => setShowNewPassword(!showNewPassword)}
              />
              <button className="primary-login-button" type="button" disabled={submitting} onClick={submitResetPassword}>
                {submitting && <span className="btn-spinner" aria-hidden="true" />}重設密碼
              </button>
            </>
          )}
        </form>

        <AuthNotice message={authMessage} error={authError} />

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

        {registerStep === 'form' ? (
          <form
            className="register-form"
            onSubmit={(event) => {
              event.preventDefault()
              submitRegister()
            }}
          >
            <FloatingField label="暱稱">
              <AuthField
                icon="user"
                value={registerName}
                onChange={setRegisterName}
                placeholder="請輸入你的暱稱"
                autoComplete="nickname"
                suffix={`${registerName.length}/20`}
              />
            </FloatingField>
            <FloatingField label="電子郵件">
              <AuthField
                icon="mail"
                value={registerEmail}
                onChange={handleRegisterEmailChange}
                placeholder="請輸入可收信的 Email"
                inputMode="email"
                autoComplete="email"
                error={registerEmailError}
                onBlur={() => { if (registerEmail && !isValidEmail(registerEmail)) setRegisterEmailError('請輸入有效的 Email 格式') }}
              />
            </FloatingField>
            <FloatingField label="密碼">
              <AuthField
                icon="lock"
                value={password}
                onChange={setPassword}
                placeholder="請設定 8 碼以上，含英文與數字"
                type={showRegisterPassword ? 'text' : 'password'}
                autoComplete="new-password"
                isPasswordToggle
                isPasswordVisible={showRegisterPassword}
                onAction={() => setShowRegisterPassword(!showRegisterPassword)}
              />
            </FloatingField>
            <PasswordStrength password={password} />
            <FloatingField label="確認密碼">
              <AuthField
                icon="lock"
                value={confirmPassword}
                onChange={handleConfirmPasswordChange}
                placeholder="請再次輸入密碼"
                type={showRegisterPassword ? 'text' : 'password'}
                autoComplete="new-password"
                error={confirmPasswordError}
                onBlur={() => { if (confirmPassword && confirmPassword !== password) setConfirmPasswordError('密碼不一致，請重新確認') }}
                isPasswordToggle
                isPasswordVisible={showRegisterPassword}
                onAction={() => setShowRegisterPassword(!showRegisterPassword)}
              />
            </FloatingField>
            <FloatingField label="出生年次">
              <AuthField icon="calendar" value={birthYear} onChange={setBirthYear} placeholder="出生年份，例如 1995" inputMode="numeric" autoComplete="bday-year" />
            </FloatingField>

            <div className="gender-field">
              <span>性別（選填）</span>
              <div className="gender-options">
                <button type="button" className={gender === 'MALE' ? 'selected' : ''} onClick={() => setGender('MALE')}>
                  <span className="gender-icon" aria-hidden="true"></span>
                  男生
                </button>
                <button type="button" className={gender === 'FEMALE' ? 'selected' : ''} onClick={() => setGender('FEMALE')}>
                  <span className="gender-icon" aria-hidden="true"></span>
                  女生
                </button>
                <button type="button" className={gender === 'PRIVATE' ? 'selected' : ''} onClick={() => setGender('PRIVATE')}>
                  <span className="gender-icon" aria-hidden="true"></span>
                  不想透露
                </button>
              </div>
            </div>

            <label className="terms-row">
              <input type="checkbox" checked={agreed} onChange={(event) => setAgreed(event.target.checked)} />
              <span>
                我已閱讀並同意{' '}
                <button type="button" onClick={() => setLegalDoc('terms')}>服務條款</button>
                {' '}與{' '}
                <button type="button" onClick={() => setLegalDoc('privacy')}>隱私權政策</button>
              </span>
            </label>

            <AuthNotice message={authMessage} error={authError} />

            <button className="register-submit" type="submit" disabled={submitting || registerName.trim().length === 0}>
              {submitting && <span className="btn-spinner" aria-hidden="true" />}{submitting ? '寄送中' : '建立帳號'}
            </button>
          </form>
        ) : (
          <form
            className="register-form"
            onSubmit={(event) => {
              event.preventDefault()
              submitRegistrationCode()
            }}
          >
            <p className="register-verify-copy">驗證碼已寄到 {registerEmail}。請輸入 6 位數驗證碼完成建立帳號。</p>
            <FloatingField label="驗證碼">
              <AuthField icon="code" value={registerCode} onChange={setRegisterCode} placeholder="6 位數驗證碼" inputMode="numeric" autoComplete="one-time-code" />
            </FloatingField>
            <div className="forgot-action-row">
              <button className="resend-button" type="button" disabled={submitting} onClick={submitRegister}>
                重新發送
              </button>
              <button className="primary-login-button compact-button" type="submit" disabled={submitting || registerCode.length < 6}>
                {submitting && <span className="btn-spinner" aria-hidden="true" />}完成註冊
              </button>
            </div>
            <button className="text-link-button" type="button" onClick={() => { resetStatus(); setRegisterStep('form') }}>
              返回修改資料
            </button>
            <AuthNotice message={authMessage} error={authError} />
          </form>
        )}

        <p className="auth-switch">
          已經有帳號了？
          <button type="button" onClick={() => setMode('login')}>
            立即登入
          </button>
        </p>
        {legalDoc && <LegalModal type={legalDoc} onClose={() => setLegalDoc(null)} />}
      </AuthFrame>
    )
  }

  return (
    <AuthFrame variant="warm">
      <LogoBlock />
      <h1 className="login-main-title">今天，也找個懂你的人</h1>
      <p className="login-subtitle">從日常共鳴開始，慢慢認識彼此</p>

      <form className="login-form" onSubmit={(event) => { event.preventDefault(); submitLogin() }}>
        <AuthField
          icon="mail"
          value={email}
          onChange={handleEmailChange}
          placeholder="Email"
          inputMode="email"
          autoComplete="email"
          error={loginEmailError}
          onBlur={() => { if (email && !isValidEmail(email)) setLoginEmailError('請輸入有效的 Email 格式') }}
        />
        <AuthField
          icon="lock"
          value={password}
          onChange={setPassword}
          placeholder="密碼"
          type={showPassword ? 'text' : 'password'}
          autoComplete="current-password"
          isPasswordToggle
          isPasswordVisible={showPassword}
          onAction={() => setShowPassword(!showPassword)}
        />
        <label className="remember-row">
          <input type="checkbox" checked={rememberMe} onChange={(e) => setRememberMe(e.target.checked)} />
          記住我
        </label>
        <AuthNotice message={authMessage} error={authError} />
        <button className="primary-login-button" type="submit" disabled={submitting || email.trim().length === 0 || password.length === 0}>
          {submitting && <span className="btn-spinner" aria-hidden="true" />}{submitting ? '登入中' : '登入'}
        </button>
      </form>

      <button className="forgot-button" type="button" onClick={openForgot}>
        忘記密碼？
      </button>

      <button className="outline-login-button" type="button" onClick={() => { resetStatus(); setMode('register') }}>
        建立帳號
      </button>

      <div className="demo-divider"><span>或快速體驗</span></div>
      <div className="demo-login-block">
        <input
          className="demo-nickname-input"
          value={demoNickname}
          onChange={(e) => setDemoNickname(e.target.value)}
          placeholder="輸入暱稱（可留空）"
          maxLength={20}
          onKeyDown={(e) => { if (e.key === 'Enter') submitDemoLogin() }}
        />
        <button className="demo-login-button" type="button" disabled={submitting} onClick={submitDemoLogin}>
          {submitting ? '…' : '試試看'}
        </button>
      </div>
    </AuthFrame>
  )
}

function getForgotDescription(step: ForgotStep) {
  if (step === 'account') {
    return '輸入你的 Email，我們會寄送驗證碼協助你重設密碼'
  }

  if (step === 'verify') {
    return '輸入收到的驗證碼，確認這是你的帳號'
  }

  return '設定新密碼後，就可以重新登入'
}

function AuthFrame({ children, variant, onBack }: { children: ReactNode; variant: 'warm' | 'clean'; onBack?: () => void }) {
  return (
    <main className={`login-screen ${variant === 'clean' ? 'register-screen' : ''}`}>
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
            <div className="login-decoration sparkle-two">✦</div>
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

type FieldIcon = 'mail' | 'user' | 'lock' | 'code' | 'calendar'

function AuthField({
  icon,
  value,
  onChange,
  placeholder,
  type = 'text',
  onAction,
  onBlur,
  suffix,
  inputMode,
  autoComplete,
  error,
  isPasswordToggle = false,
  isPasswordVisible = false,
}: {
  icon: FieldIcon
  value: string
  onChange: (value: string) => void
  placeholder: string
  type?: string
  onAction?: () => void
  onBlur?: () => void
  suffix?: string
  inputMode?: 'text' | 'email' | 'numeric'
  autoComplete?: string
  error?: string
  isPasswordToggle?: boolean
  isPasswordVisible?: boolean
}) {
  return (
    <label className={`login-field${error ? ' has-error' : ''}`}>
      <span className="field-icon" aria-hidden="true">
        <FieldIconView icon={icon} />
      </span>
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        type={type}
        inputMode={inputMode}
        autoComplete={autoComplete}
        onBlur={onBlur}
      />
      {error && <span className="field-error">{error}</span>}
      {suffix && <span className="field-suffix">{suffix}</span>}
      {isPasswordToggle && (
        <button className={`field-action eye-action ${isPasswordVisible ? 'visible' : ''}`} type="button" onClick={onAction} aria-label="切換密碼顯示">
          {isPasswordVisible ? <Eye size={23} strokeWidth={1.9} /> : <EyeOff size={23} strokeWidth={1.9} />}
        </button>
      )}
    </label>
  )
}

function FieldIconView({ icon }: { icon: FieldIcon }) {
  switch (icon) {
    case 'user':
      return <UserRound size={23} strokeWidth={1.8} />
    case 'lock':
      return <LockKeyhole size={23} strokeWidth={1.8} />
    case 'code':
      return <ShieldCheck size={23} strokeWidth={1.8} />
    case 'calendar':
      return <CalendarDays size={23} strokeWidth={1.8} />
    case 'mail':
    default:
      return <Mail size={23} strokeWidth={1.8} />
  }
}

function FloatingField({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="floating-field">
      <span>{label}</span>
      {children}
    </div>
  )
}

function PasswordStrength({ password }: { password: string }) {
  if (!password) return null
  const score = [password.length >= 8, /[a-zA-Z]/.test(password), /[0-9]/.test(password)].filter(Boolean).length
  const level = score === 3 ? 'strong' : score === 2 ? 'medium' : 'weak'
  const label = score === 3 ? '強' : score === 2 ? '中' : '弱'
  return (
    <div className={`password-strength ${level}`}>
      <div className="strength-bars">
        <span /><span /><span />
      </div>
      <em>{label}</em>
    </div>
  )
}

function AuthNotice({ message, error }: { message: string; error: string }) {
  if (!message && !error) {
    return null
  }

  return <p className={`auth-notice ${error ? 'error' : ''}`}>{error || message}</p>
}

function LegalModal({ type, onClose }: { type: LegalDoc; onClose: () => void }) {
  const doc = type === 'terms' ? termsDocument : privacyDocument

  return (
    <div className="legal-modal-backdrop" role="presentation" onMouseDown={onClose}>
      <section className="legal-modal" role="dialog" aria-modal="true" aria-labelledby="legal-title" onMouseDown={(event) => event.stopPropagation()}>
        <div className="legal-modal-header">
          <div>
            <p>同頻 Today</p>
            <h2 id="legal-title">{doc.title}</h2>
          </div>
          <button type="button" onClick={onClose} aria-label="關閉">
            ×
          </button>
        </div>
        <p className="legal-updated">最後更新：2026 年 6 月 1 日</p>
        <div className="legal-modal-body">
          {doc.sections.map((section) => (
            <section key={section.heading}>
              <h3>{section.heading}</h3>
              {section.items.map((item) => (
                <p key={item}>{item}</p>
              ))}
            </section>
          ))}
        </div>
      </section>
    </div>
  )
}

const termsDocument = {
  title: '服務條款',
  sections: [
    {
      heading: '一、服務性質與適用範圍',
      items: [
        '同頻 Today（以下稱「本服務」）為生活共鳴型社交應用程式，協助使用者透過今日事件、情緒標籤與低壓任務與他人建立連結。本服務不保證配對、交友、約會或任何特定關係之結果。',
        '本條款適用於所有透過本服務網站、應用程式或相關平台存取本服務之使用者（以下稱「使用者」）。使用者建立帳號、登入或以任何方式使用本服務，即表示已閱讀、理解並同意本條款及隱私權政策之全部內容。'
      ]
    },
    {
      heading: '二、帳號建立與管理責任',
      items: [
        '使用者須年滿十三歲，或已取得法定代理人之明示同意，始得使用本服務。使用者應確保所提供之電子郵件真實有效且可正常收信，並自行妥善保管帳號密碼及驗證碼，對其帳號下之一切活動負完全責任。',
        '使用者不得冒用他人身分、盜用他人電子郵件，或透過自動化程式、機器人或任何非人工方式批量建立帳號。發現帳號遭未經授權使用時，應立即通知本服務；本服務接獲通知前所生之損失，由使用者自行承擔。',
        '使用者不得將帳號轉讓、出借、出租或授權任何第三人使用。'
      ]
    },
    {
      heading: '三、使用規範與禁止行為',
      items: [
        '使用者不得發表下列內容：指名人身攻擊、種族或性別歧視之仇恨言論、性騷擾、跟蹤騷擾、暴力威脅、煽動自傷或自殺、散布他人個人資料（包括姓名、電話、住址、身分證字號、車牌等可識別個人之資訊）。',
        '使用者不得將本服務用於詐騙、非法借貸、投資勸誘、加密貨幣招募、未經授權之商業廣告、徵才招募、洗版連結或任何違反中華民國法律之行為。',
        '使用者不得上傳含有惡意程式碼、病毒或任何干擾本服務正常運作之內容，亦不得以任何技術手段嘗試入侵、破壞或未授權存取本服務系統。'
      ]
    },
    {
      heading: '四、使用者內容與智慧財產',
      items: [
        '使用者保留其發布內容之著作權及其他相關權利。使用者發布內容時，即授予本服務非獨家、全球性、免版稅、可轉授權之授權，用以展示、儲存、備份、傳輸、安全審查及提供本服務所必要之處理，前述授權於使用者刪除該內容後終止。',
        '本服務之名稱、標誌、商標、介面設計、原始程式碼及其他相關智慧財產權，均為本服務所有或依法取得授權使用。使用者未經本服務書面授權，不得重製、改作、散布或以任何方式利用上述內容。'
      ]
    },
    {
      heading: '五、內容審核與帳號處置',
      items: [
        '本服務得對使用者發布之內容進行審核，對於疑似違規或具高風險之內容，得不經通知逕行限制顯示、隱藏或刪除，並得暫停或終止違規帳號，保留必要紀錄以利後續調查或法律程序。',
        '使用者得隨時向本服務申請刪除帳號，本服務將依隱私權政策之規定處理相關個人資料。帳號刪除後，使用者所發布之公開內容得依本服務之資料保存政策保留特定期間。'
      ]
    },
    {
      heading: '六、服務中斷、變更與終止',
      items: [
        '本服務得因系統維護、安全事由、第三方服務異常或不可抗力（包括天災、戰爭、政府命令等）暫時中斷，本服務將盡合理努力事先通知，但不對中斷期間之損失負賠償責任。',
        '本服務保留隨時新增、修改或終止任何功能之權利，並得於通知使用者後終止整體服務。'
      ]
    },
    {
      heading: '七、免責聲明',
      items: [
        '本服務以「現況」及「現有」基礎提供，不作任何明示或默示之保證，包括但不限於適售性、特定目的適用性或不侵權之保證。',
        '本服務對使用者間之互動、線下見面、任務參與所生之人身安全或財產損失不負保證責任，使用者應自行審慎評估風險。本服務亦不保證配對結果之準確性或使用者資料之真實性。'
      ]
    },
    {
      heading: '八、賠償責任限制',
      items: [
        '在中華民國法律允許之最大範圍內，本服務對於因使用或無法使用本服務所生之間接、偶發、特殊、懲罰性或衍生性損害（包括利潤損失、資料遺失、商譽損害等）不負任何賠償責任，不論是否已被告知此類損害之可能性。',
        '如本服務依法應負賠償責任，其賠償總額以使用者於事件發生前十二個月內實際支付予本服務之費用為上限；如為免費服務，則以新臺幣一千元為上限。'
      ]
    },
    {
      heading: '九、條款修改',
      items: [
        '本服務得因功能調整、法規變更或營運需求修改本條款，並於生效前以站內公告或電子郵件通知使用者，重大變更之通知期間不少於七日。使用者於通知期間屆滿後繼續使用本服務，視為同意修改後之條款。如使用者不同意修改內容，得於通知期間內刪除帳號。'
      ]
    },
    {
      heading: '十、準據法與爭議解決',
      items: [
        '本條款之訂立、解釋及執行，均依中華民國法律為準據法。因本條款或本服務所生之一切爭議，雙方同意以臺灣臺北地方法院為第一審管轄法院；消費者保護法另有規定者，從其規定。'
      ]
    }
  ]
}

const privacyDocument = {
  title: '隱私權政策',
  sections: [
    {
      heading: '一、資料控管者與適用範圍',
      items: [
        '本隱私權政策（以下稱「本政策」）由同頻 Today 服務提供方（以下稱「本公司」）依中華民國個人資料保護法（以下稱「個資法」）及相關法規制定，說明本公司如何蒐集、處理、利用及保護使用者之個人資料。',
        '本政策適用於同頻 Today 所提供之網站、漸進式網路應用程式（PWA）、API 及一切相關服務。使用者使用本服務，即表示同意本政策之全部內容。如對本政策有任何疑問，請透過本服務客服管道聯繫本公司。'
      ]
    },
    {
      heading: '二、蒐集資料之類別',
      items: [
        '帳號識別資料：電子郵件地址、暱稱、密碼雜湊值（以 bcrypt 演算法不可逆處理，本公司不持有明文密碼）、出生年次、性別（選填）、服務條款同意時間戳記、電子郵件驗證及密碼重設紀錄。',
        '服務使用資料：今日事件描述、情緒與興趣標籤、樹洞文章與留言內容、任務參與紀錄、聊天文字訊息、使用者發起之檢舉紀錄及內容安全審查結果。',
        '技術與存取資料：IP 位址、瀏覽器類型與版本、作業系統或裝置資訊、存取時間與時區、API 請求紀錄、錯誤日誌，以及瀏覽器本地儲存（localStorage／sessionStorage）中之登入狀態資料。'
      ]
    },
    {
      heading: '三、蒐集目的與法律依據',
      items: [
        '本公司依個資法第十九條規定，基於下列特定目的蒐集並處理個人資料：（一）帳號建立、身分驗證與登入管理（契約履行必要）；（二）電子郵件驗證及密碼重設（契約履行必要）；（三）今日共鳴配對演算與服務功能提供（契約履行必要）；（四）服務品質改善、資安維護及濫用防制（正當利益）；（五）依法律規定履行通報或提供資料之義務（法律遵循）。',
        '使用者得選擇不提供特定資料，但若拒絕提供帳號建立或驗證所必需之資料，將無法使用帳號登入、配對、聊天或其他需要帳號之功能。'
      ]
    },
    {
      heading: '四、資料之使用與分享',
      items: [
        '本公司不會出售、出租或以商業方式提供使用者個人資料予任何第三方。',
        '本公司得將個人資料提供予下列對象：（一）依契約受本公司委託處理資料之主機代管、資料庫、電子郵件寄送及資安服務廠商，該等廠商均受保密義務約束，僅得於委託範圍內使用資料；（二）依法令、法院裁判或主管機關命令要求時；（三）為防止詐欺、重大安全威脅或維護使用者生命身體安全所必要時。',
        '當本公司涉及合併、收購或資產轉讓時，將事先通知使用者，且受讓方須承繼本政策對使用者個人資料之保護義務。'
      ]
    },
    {
      heading: '五、資料保存期間',
      items: [
        '帳號識別資料：保存至帳號刪除後六個月，以利爭議處理及法律義務之履行。',
        '服務使用資料（文章、留言、聊天訊息）：保存至使用者請求刪除或本服務終止，惟經檢舉或涉及安全調查之內容得依法延長保存。',
        '技術日誌與存取紀錄：保存十二個月後刪除或去識別化。逾上述期間或無繼續保存必要時，本公司將以刪除、銷毀或不可逆去識別化方式處理個人資料。'
      ]
    },
    {
      heading: '六、使用者之權利',
      items: [
        '依個資法第三條，使用者就其個人資料享有下列權利，並得隨時向本公司行使：（一）查詢或請求閱覽；（二）請求製給複製本；（三）請求補充或更正；（四）請求停止蒐集、處理或利用；（五）請求刪除。',
        '本公司將於收到書面或電子請求後三十日內回覆，如需延長處理期間，將事先通知使用者。行使上述權利如須負擔必要成本，本公司將事先告知收費標準。'
      ]
    },
    {
      heading: '七、安全措施',
      items: [
        '本公司採取符合業界標準之安全措施保護使用者個人資料，包括：密碼以 bcrypt 演算法雜湊儲存、驗證碼設有時效限制並單次使用後即失效、資料傳輸採 TLS 加密、資料庫存取實施最小權限原則，以及定期安全審查。',
        '儘管本公司已採取前述措施，網際網路傳輸無法保證絕對安全。如發生個人資料外洩事件，本公司將依個資法規定於知悉後採取必要補救措施，並依法通知受影響之使用者及主管機關。'
      ]
    },
    {
      heading: '八、未成年人保護',
      items: [
        '本服務僅提供年滿十八歲之使用者註冊與使用。若本公司發現使用者未滿十八歲，得限制或終止其帳號使用，並依適用法令刪除或停止處理相關個人資料。'
      ]
    },
    {
      heading: '九、Cookie 與本地儲存',
      items: [
        '本服務使用瀏覽器 localStorage 或 sessionStorage 儲存使用者登入狀態，不使用第三方廣告追蹤 Cookie。使用者得隨時透過瀏覽器設定清除本地儲存資料，惟清除後需重新登入。'
      ]
    },
    {
      heading: '十、政策更新',
      items: [
        '本公司得因服務功能調整、法規變更或資料處理實務更新本政策。重大變更（包括蒐集目的擴張、新增資料分享對象等）將於生效前至少七日以站內公告或電子郵件通知使用者。使用者於通知期間屆滿後繼續使用本服務，視為同意更新後之政策。本政策之最後更新日期標示於文件頂端。'
      ]
    }
  ]
}
