import { useEffect, useRef, useState } from 'react'
import { Mail } from 'lucide-react'
import { SocialLogin } from '@capgo/capacitor-social-login'
import { ensureSocialInit, GOOGLE_WEB_CLIENT_ID, FACEBOOK_APP_ID } from './googleAuth'
import {
  confirmRegistration,
  confirmPasswordReset,
  demoLogin,
  googleLogin,
  facebookLogin,
  loginAccount,
  registerAccount,
  requestPasswordReset,
  verifyPasswordReset,
} from './api'
import type { DemoUser } from './types'
import type { LegalDoc } from './legalDocuments'
import { APP_VERSION_LABEL } from '../../shared/lib/appVersion'
import {
  AuthFrame,
  LogoBlock,
  AuthField,
  AuthNotice,
  FloatingField,
  PasswordStrength,
  PasswordHint,
  LegalModal,
  getForgotDescription,
  type ForgotStep,
  type HintState,
} from './AuthControls'

// 密碼規則的單一來源：提示小窗、欄位錯誤、送出前檢查都讀這一份，不會各寫各的。
// 內容必須與後端 AuthService.ValidatePassword 一致——兩邊不同步會變成前端放行、
// 後端退回，使用者只看得到一條講不清原因的錯誤。
const PASSWORD_RULES = [
  {
    label: '8 到 64 碼',
    error: '密碼需為 8 到 64 碼',
    test: (value: string) => value.length >= 8 && value.length <= 64,
  },
  {
    label: '英文與數字都要有',
    error: '密碼需同時包含英文與數字',
    test: (value: string) => /[A-Za-z]/.test(value) && /[0-9]/.test(value),
  },
]

function passwordRuleStates(value: string) {
  return PASSWORD_RULES.map((rule) => ({ label: rule.label, passed: rule.test(value) }))
}

/**
 * 「點一下浮出、過幾秒自己消失」的提示小窗狀態。
 *
 * leaving 是給淡出動畫用的中繼狀態；少了它就會直接閃不見。
 * 重新聚焦會重設計時器，所以反覆點欄位不會累積一堆 timeout。
 */
function useAutoHideHint(visibleMs = 5000, fadeMs = 300) {
  const [state, setState] = useState<HintState>('hidden')
  const timers = useRef<number[]>([])

  function clearTimers() {
    timers.current.forEach(clearTimeout)
    timers.current = []
  }

  function restartCountdown() {
    clearTimers()
    timers.current.push(
      window.setTimeout(() => setState('leaving'), visibleMs),
      window.setTimeout(() => setState('hidden'), visibleMs + fadeMs),
    )
  }

  // 元件卸載時收掉計時器，避免在已卸載的元件上 setState
  useEffect(() => () => timers.current.forEach(clearTimeout), [])

  return {
    state,
    show() {
      setState('shown')
      restartCountdown()
    },
    /**
     * 打字時把倒數往後推，小窗才不會在使用者正在輸入時消失——
     * 勾選清單最有用的時候正是他邊打邊看還差哪一項。
     * 已經收掉的就不再叫回來：每打一個字彈一次會很吵。
     */
    keepAlive() {
      if (state === 'hidden') {
        return
      }
      setState('shown')
      restartCountdown()
    },
    hide() {
      clearTimers()
      setState('hidden')
    },
  }
}

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

function isAdultBirthYear(birthYear: string) {
  const normalized = birthYear.replace(/\D/g, '').slice(0, 4)
  if (normalized.length < 4) return false

  const year = Number(normalized)
  const currentYear = new Date().getFullYear()
  return year >= 1940 && year <= currentYear - 18
}

export default function LoginPage({ onAuthenticated }: Props) {
  const [mode, setModeState] = useState<AuthMode>('welcome')
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
  const [passwordError, setPasswordError] = useState('')
  const [registerNameError, setRegisterNameError] = useState('')
  const [newPasswordError, setNewPasswordError] = useState('')
  const registerPasswordHint = useAutoHideHint()
  const resetPasswordHint = useAutoHideHint()

  /**
   * 切換登入／註冊／忘記密碼畫面。
   *
   * 三個畫面共用同一組密碼欄位 state，不在切換時清掉的話，
   * 剛剛在註冊頁打到一半的密碼會原封不動出現在登入頁的密碼欄。
   * 切換點有八處，所以包成一個函式——真正的 setter 只在這裡用，
   * 日後新增入口照樣呼叫 setMode 就不會漏掉。
   */
  function setMode(next: AuthMode) {
    setModeState(next)
    setPassword('')
    setConfirmPassword('')
    setNewPassword('')
    setPasswordError('')
    setConfirmPasswordError('')
    setNewPasswordError('')
  }

  function resetStatus() {
    setAuthMessage('')
    setAuthError('')
  }

  // 送出後的錯誤橫幅講的是「上一次送出」的結果；使用者一開始修改欄位，那條訊息就過期了。
  // 不清掉會讓人以為改了也沒用——這正是註冊看起來「卡死」的體感來源。
  function clearAuthError() {
    if (authError) setAuthError('')
  }

  function passwordRuleError(value: string): string {
    return PASSWORD_RULES.find((rule) => !rule.test(value))?.error ?? ''
  }

  function isValidEmail(value: string) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
  }

  // 與後端 ValidateRegister 的暱稱規則一致（2 到 20 個字）。
  // 先前前端只擋空白，1 個字要送到後端才被退回，錯誤還落在表單最底下。
  function nicknameRuleError(value: string): string {
    const trimmed = value.trim()
    return trimmed.length >= 2 && trimmed.length <= 20 ? '' : '暱稱需為 2 到 20 個字'
  }

  function handleRegisterNameChange(value: string) {
    setRegisterName(value)
    clearAuthError()
    if (registerNameError) setRegisterNameError(nicknameRuleError(value))
  }

  function handleEmailChange(value: string) {
    setEmail(value)
    if (loginEmailError) setLoginEmailError('')
  }

  function handleRegisterEmailChange(value: string) {
    setRegisterEmail(value)
    clearAuthError()
    if (registerEmailError) setRegisterEmailError('')
  }

  // 兩個密碼欄互相依賴，任一邊變動都要用「這次的新值」重新比對。
  // 先前只在離開確認密碼欄時檢查、且只有編輯確認密碼才清除錯誤，
  // 導致「先填確認密碼 → 再補完密碼」時，兩邊明明一致卻一直顯示不一致。
  function syncPasswordMatch(nextPassword: string, nextConfirm: string) {
    if (!nextConfirm) {
      setConfirmPasswordError('')
      return
    }
    setConfirmPasswordError(nextPassword === nextConfirm ? '' : '密碼不一致，請重新確認')
  }

  function handlePasswordChange(value: string) {
    setPassword(value)
    clearAuthError()
    registerPasswordHint.keepAlive()
    syncPasswordMatch(value, confirmPassword)
    // 只在「已經顯示錯誤」時即時重算，使用者一改好就立刻消失；
    // 還沒出錯就不要邊打邊唸，剛輸入第一個字元時長度本來就不足。
    if (passwordError) setPasswordError(passwordRuleError(value))
  }

  function handleConfirmPasswordChange(value: string) {
    setConfirmPassword(value)
    clearAuthError()
    syncPasswordMatch(password, value)
  }

  // 重設密碼那一步用的是 newPassword，但共用同一個「確認密碼」欄位
  function handleNewPasswordChange(value: string) {
    setNewPassword(value)
    clearAuthError()
    resetPasswordHint.keepAlive()
    syncPasswordMatch(value, confirmPassword)
    if (newPasswordError) setNewPasswordError(passwordRuleError(value))
  }

  function handleResetConfirmChange(value: string) {
    setConfirmPassword(value)
    clearAuthError()
    syncPasswordMatch(newPassword, value)
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
      // 格式明顯不對就不用跑一趟後端，錯誤也標在 Email 欄而不是表單底部
      if (!isValidEmail(email)) {
        setLoginEmailError('請輸入有效的 Email 格式')
        throw new Error('請輸入有效的 Email 格式。')
      }
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

  // 社群登入：原生喚起 → 取得憑證（Google idToken / Facebook accessToken）→ 後端驗證後簽發 JWT。
  function handleSocialLogin(provider: 'google' | 'facebook') {
    if (provider === 'facebook') {
      if (!FACEBOOK_APP_ID) {
        setAuthMessage('')
        setAuthError('Facebook 登入尚未設定（缺少 App ID），請先用 Google 或 Email 登入。')
        return
      }
      void runAuthAction(async () => {
        await ensureSocialInit()
        const res = await SocialLogin.login({ provider: 'facebook', options: { permissions: ['email', 'public_profile'] } })
        const accessToken = (res?.result as { accessToken?: { token?: string } } | undefined)?.accessToken?.token
        if (!accessToken) {
          throw new Error('沒有取得 Facebook 登入憑證，請再試一次。')
        }
        const user = await facebookLogin(accessToken, rememberMe)
        onAuthenticated(user, rememberMe)
      })
      return
    }

    if (!GOOGLE_WEB_CLIENT_ID) {
      setAuthMessage('')
      setAuthError('Google 登入尚未設定（缺少 Web Client ID），請先用 Email 登入或快速體驗。')
      return
    }

    void runAuthAction(async () => {
      await ensureSocialInit()
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

      // 每一項都先在前端擋下來，並把錯誤標在對應欄位上。
      // 否則使用者只會在表單底部看到一句不知道該改哪裡的訊息——
      // 而那正是「改了半天還是過不去」的來源。
      const nameError = nicknameRuleError(registerName)
      if (nameError) {
        setRegisterNameError(nameError)
        throw new Error(`${nameError}。`)
      }
      if (!isValidEmail(registerEmail)) {
        setRegisterEmailError('請輸入有效的 Email 格式')
        throw new Error('請輸入有效的 Email 格式。')
      }
      if (!isAdultBirthYear(birthYear)) {
        throw new Error('未滿 18 歲暫時不能使用同頻 Today。')
      }

      const ruleError = passwordRuleError(password)
      if (ruleError) {
        setPasswordError(ruleError)
        throw new Error(`${ruleError}。`)
      }
      if (password !== confirmPassword) {
        setConfirmPasswordError('密碼不一致，請重新確認')
        throw new Error('兩次輸入的密碼不一致。')
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
      if (!isValidEmail(email)) {
        setLoginEmailError('請輸入有效的 Email 格式')
        throw new Error('請輸入有效的 Email 格式。')
      }
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
      // 與註冊頁同一套檢查：這裡同樣是在「設定密碼」，少了它就會重現
      // 「按了沒反應、底部跳一句看不懂的話」那個狀況。
      const ruleError = passwordRuleError(newPassword)
      if (ruleError) {
        setNewPasswordError(ruleError)
        throw new Error(`${ruleError}。`)
      }
      if (newPassword !== confirmPassword) {
        setConfirmPasswordError('密碼不一致，請重新確認')
        throw new Error('兩次輸入的密碼不一致。')
      }
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
          {/* 測試期間顯示版本：回報問題時能直接對上是哪一版，不必先登入進設定頁 */}
          <span className="welcome-version">{APP_VERSION_LABEL}</span>
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
              {/* 這一步同樣要符合密碼規則，所以用同一組提示；外層 div 只是給小窗定位用 */}
              <div className="password-hint-anchor">
                <AuthField
                  icon="lock"
                  value={newPassword}
                  onChange={handleNewPasswordChange}
                  placeholder="新密碼"
                  type={showNewPassword ? 'text' : 'password'}
                  autoComplete="new-password"
                  error={newPasswordError}
                  onFocus={resetPasswordHint.show}
                  onBlur={() => {
                    resetPasswordHint.hide()
                    setNewPasswordError(newPassword ? passwordRuleError(newPassword) : '')
                  }}
                  isPasswordToggle
                  isPasswordVisible={showNewPassword}
                  onAction={() => setShowNewPassword(!showNewPassword)}
                />
                <PasswordHint state={resetPasswordHint.state} rules={passwordRuleStates(newPassword)} />
              </div>
              <AuthField
                icon="lock"
                value={confirmPassword}
                onChange={handleResetConfirmChange}
                placeholder="確認新密碼"
                type={showNewPassword ? 'text' : 'password'}
                autoComplete="new-password"
                error={confirmPasswordError}
                onBlur={() => syncPasswordMatch(newPassword, confirmPassword)}
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
                onChange={handleRegisterNameChange}
                placeholder="請輸入你的暱稱"
                autoComplete="nickname"
                suffix={`${registerName.length}/20`}
                error={registerNameError}
                onBlur={() => setRegisterNameError(registerName ? nicknameRuleError(registerName) : '')}
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
                onChange={handlePasswordChange}
                placeholder="請設定 8 碼以上，含英文與數字"
                type={showRegisterPassword ? 'text' : 'password'}
                autoComplete="new-password"
                error={passwordError}
                onFocus={registerPasswordHint.show}
                onBlur={() => {
                  registerPasswordHint.hide()
                  setPasswordError(password ? passwordRuleError(password) : '')
                }}
                isPasswordToggle
                isPasswordVisible={showRegisterPassword}
                onAction={() => setShowRegisterPassword(!showRegisterPassword)}
              />
              <PasswordHint state={registerPasswordHint.state} rules={passwordRuleStates(password)} />
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
                onBlur={() => syncPasswordMatch(password, confirmPassword)}
                isPasswordToggle
                isPasswordVisible={showRegisterPassword}
                onAction={() => setShowRegisterPassword(!showRegisterPassword)}
              />
            </FloatingField>
            <FloatingField label="出生年次">
              <AuthField icon="calendar" value={birthYear} onChange={(value) => { clearAuthError(); setBirthYear(value) }} placeholder="出生年份，例如 1995" inputMode="numeric" autoComplete="bday-year" />
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
              <input type="checkbox" checked={agreed} onChange={(event) => { clearAuthError(); setAgreed(event.target.checked) }} />
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
