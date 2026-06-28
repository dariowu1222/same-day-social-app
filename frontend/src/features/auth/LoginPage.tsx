import { useState } from 'react'
import { Mail } from 'lucide-react'
import { SocialLogin } from '@capgo/capacitor-social-login'
import {
  confirmRegistration,
  confirmPasswordReset,
  demoLogin,
  googleLogin,
  loginAccount,
  registerAccount,
  requestPasswordReset,
  verifyPasswordReset,
} from './api'
import type { DemoUser } from './types'
import type { LegalDoc } from './legalDocuments'
import {
  AuthFrame,
  LogoBlock,
  AuthField,
  AuthNotice,
  FloatingField,
  PasswordStrength,
  LegalModal,
  getForgotDescription,
  type ForgotStep,
} from './AuthControls'

type AuthMode = 'welcome' | 'login' | 'forgot' | 'register'
type RegisterStep = 'form' | 'verify'

type Props = {
  onAuthenticated: (user: DemoUser, remember: boolean) => void
}

// 品牌圖示（官方配色），社群登入按鈕用
function GoogleIcon() {
  return (
    <svg width="22" height="22" viewBox="0 0 48 48" aria-hidden="true">
      <path fill="#4285F4" d="M45.12 24.5c0-1.56-.14-3.06-.4-4.5H24v8.51h11.84c-.51 2.75-2.06 5.08-4.39 6.64v5.52h7.11c4.16-3.83 6.56-9.47 6.56-16.17z" />
      <path fill="#34A853" d="M24 46c5.94 0 10.92-1.97 14.56-5.33l-7.11-5.52c-1.97 1.32-4.49 2.1-7.45 2.1-5.73 0-10.58-3.87-12.31-9.07H4.34v5.7C7.96 41.07 15.4 46 24 46z" />
      <path fill="#FBBC05" d="M11.69 28.18c-.44-1.32-.69-2.73-.69-4.18s.25-2.86.69-4.18v-5.7H4.34A21.98 21.98 0 0 0 2 24c0 3.55.85 6.91 2.34 9.88l7.35-5.7z" />
      <path fill="#EA4335" d="M24 10.75c3.23 0 6.13 1.11 8.41 3.29l6.31-6.31C34.91 4.18 29.93 2 24 2 15.4 2 7.96 6.93 4.34 14.12l7.35 5.7c1.73-5.2 6.58-9.07 12.31-9.07z" />
    </svg>
  )
}

function FacebookIcon() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" aria-hidden="true">
      <path fill="#fff" d="M24 12.07C24 5.4 18.63 0 12 0S0 5.4 0 12.07c0 6.02 4.39 11.01 10.13 11.93v-8.44H7.08v-3.49h3.05V9.41c0-3.02 1.79-4.69 4.53-4.69 1.31 0 2.68.24 2.68.24v2.97h-1.51c-1.49 0-1.95.93-1.95 1.88v2.26h3.32l-.53 3.49h-2.79V24C19.61 23.08 24 18.09 24 12.07z" />
    </svg>
  )
}

// Google Web Client ID（在 Google Cloud Console 建立 OAuth Web client 後填入 .env：VITE_GOOGLE_WEB_CLIENT_ID）
const GOOGLE_WEB_CLIENT_ID = import.meta.env.VITE_GOOGLE_WEB_CLIENT_ID as string | undefined
let socialInitialized = false
async function ensureGoogleInit() {
  if (socialInitialized) return
  await SocialLogin.initialize({ google: { webClientId: GOOGLE_WEB_CLIENT_ID! } })
  socialInitialized = true
}

function isAdultBirthYear(birthYear: string) {
  const normalized = birthYear.replace(/\D/g, '').slice(0, 4)
  if (normalized.length < 4) return false

  const year = Number(normalized)
  const currentYear = new Date().getFullYear()
  return year >= 1940 && year <= currentYear - 18
}

export default function LoginPage({ onAuthenticated }: Props) {
  const [mode, setMode] = useState<AuthMode>('welcome')
  const [optionsOpen, setOptionsOpen] = useState(false)
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
      const user = await demoLogin('同頻使用者')
      onAuthenticated(user, false)
    })
  }

  // Facebook 尚未實作；Google 走原生喚起 → 取得 idToken → 後端驗證後簽發 JWT。
  function handleSocialLogin(provider: 'google' | 'facebook') {
    if (provider === 'facebook') {
      setAuthError('')
      setAuthMessage('Facebook 登入即將推出，目前請先用 Google 或 Email 登入。')
      return
    }

    if (!GOOGLE_WEB_CLIENT_ID) {
      setAuthMessage('')
      setAuthError('Google 登入尚未設定（缺少 Web Client ID），請先用 Email 登入或快速體驗。')
      return
    }

    void runAuthAction(async () => {
      await ensureGoogleInit()
      // 不傳 scopes：基本登入已含 email/profile，傳自訂 scopes 需改 MainActivity（外掛限制）
      const res = await SocialLogin.login({ provider: 'google', options: {} })
      const idToken = (res?.result as { idToken?: string } | undefined)?.idToken
      if (!idToken) {
        throw new Error('沒有取得 Google 登入憑證，請再試一次。')
      }
      const user = await googleLogin(idToken, rememberMe)
      onAuthenticated(user, rememberMe)
    })
  }

  // 從歡迎頁的三選項進入 Email 帳密登入畫面
  function openEmailLogin() {
    resetStatus()
    setOptionsOpen(false)
    setMode('login')
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

  if (mode === 'welcome') {
    return (
      <AuthFrame variant="warm" center>
        {/* 點任意處 → 跳出登入選項 sheet */}
        <button
          className="welcome-hero"
          type="button"
          onClick={() => { resetStatus(); setOptionsOpen(true) }}
          aria-label="開始登入"
        >
          <LogoBlock />
          <h1 className="login-main-title">今天，也找個懂你的人</h1>
          <p className="login-subtitle">從日常共鳴開始，慢慢認識彼此</p>
          <span className="welcome-cta">
            輕觸畫面，開始同頻
            <span className="welcome-cta-arrow" aria-hidden="true">↑</span>
          </span>
        </button>

        {/* 登入方式 Bottom Sheet */}
        <div
          className={`auth-options-mask${optionsOpen ? ' open' : ''}`}
          onClick={() => setOptionsOpen(false)}
        />
        <div className={`auth-options-sheet${optionsOpen ? ' open' : ''}`} role="dialog" aria-label="選擇登入方式">
          <div className="auth-options-handle" />
          <p className="auth-options-title">登入 / 註冊同頻 Today</p>

          <button className="social-button google" type="button" onClick={() => handleSocialLogin('google')}>
            <span className="social-icon" aria-hidden="true"><GoogleIcon /></span>
            使用 Google 繼續
          </button>
          <button className="social-button facebook" type="button" onClick={() => handleSocialLogin('facebook')}>
            <span className="social-icon" aria-hidden="true"><FacebookIcon /></span>
            使用 Facebook 繼續
          </button>
          <button className="social-button email" type="button" onClick={openEmailLogin}>
            <span className="social-icon" aria-hidden="true"><Mail size={22} strokeWidth={1.9} /></span>
            使用 Email 登入
          </button>

          <AuthNotice message={authMessage} error={authError} />

          <div className="auth-options-foot">
            <button type="button" onClick={() => { resetStatus(); setOptionsOpen(false); setMode('register') }}>
              建立帳號
            </button>
            <span className="dot" aria-hidden="true">·</span>
            <button type="button" onClick={submitDemoLogin} disabled={submitting}>
              {submitting ? '進入中…' : '快速體驗'}
            </button>
          </div>
        </div>
      </AuthFrame>
    )
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
    <AuthFrame variant="warm" onBack={() => { resetStatus(); setMode('welcome') }}>
      <LogoBlock compact />
      <h1 className="login-main-title">歡迎回來</h1>
      <p className="login-subtitle">用 Email 登入同頻 Today</p>

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
    </AuthFrame>
  )
}
