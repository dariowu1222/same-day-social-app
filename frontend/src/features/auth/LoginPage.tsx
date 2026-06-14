import { useState } from 'react'
import {
  confirmRegistration,
  confirmPasswordReset,
  demoLogin,
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

type AuthMode = 'login' | 'forgot' | 'register'
type RegisterStep = 'form' | 'verify'

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
