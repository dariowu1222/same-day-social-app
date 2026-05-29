import { useState } from 'react'
import type { ReactNode } from 'react'
import { CalendarDays, Eye, EyeOff, LockKeyhole, Mail, ShieldCheck, UserRound } from 'lucide-react'
import {
  confirmRegistration,
  confirmPasswordReset,
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
  onAuthenticated: (user: DemoUser) => void
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

  function resetStatus() {
    setAuthMessage('')
    setAuthError('')
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
      const user = await loginAccount({ email, password })
      onAuthenticated(user)
    })
  }

  async function submitRegister() {
    await runAuthAction(async () => {
      if (!agreed) {
        throw new Error('請先閱讀並同意服務條款與隱私權政策。')
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
      const user = await confirmRegistration({ email: registerEmail, code: registerCode })
      onAuthenticated(user)
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
      onAuthenticated(user)
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
          <span className={forgotStep === 'account' ? 'active' : ''}>1</span>
          <span className={forgotStep === 'verify' ? 'active' : ''}>2</span>
          <span className={forgotStep === 'reset' ? 'active' : ''}>3</span>
        </div>

        <form className="auth-card forgot-card" onSubmit={(event) => event.preventDefault()}>
          {forgotStep === 'account' && (
            <>
              <AuthField icon="mail" value={email} onChange={setEmail} placeholder="Email" inputMode="email" />
              <button className="primary-login-button" type="button" disabled={submitting || !email.trim()} onClick={submitResetRequest}>
                發送驗證碼
              </button>
            </>
          )}

          {forgotStep === 'verify' && (
            <>
              <AuthField icon="code" value={resetCode} onChange={setResetCode} placeholder="6 位數驗證碼" inputMode="numeric" />
              <div className="forgot-action-row">
                <button className="resend-button" type="button" disabled={submitting} onClick={submitResetRequest}>
                  重新發送
                </button>
                <button className="primary-login-button compact-button" type="button" disabled={submitting || resetCode.length < 6} onClick={submitVerifyCode}>
                  下一步
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
                isPasswordToggle
                isPasswordVisible={showNewPassword}
                onAction={() => setShowNewPassword(!showNewPassword)}
              />
              <button className="primary-login-button" type="button" disabled={submitting} onClick={submitResetPassword}>
                重設密碼
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
                suffix={`${registerName.length}/20`}
              />
            </FloatingField>
            <FloatingField label="電子郵件">
              <AuthField icon="mail" value={registerEmail} onChange={setRegisterEmail} placeholder="請輸入可收信的 Email" inputMode="email" />
            </FloatingField>
            <FloatingField label="密碼">
              <AuthField
                icon="lock"
                value={password}
                onChange={setPassword}
                placeholder="請設定 8 碼以上，含英文與數字"
                type={showRegisterPassword ? 'text' : 'password'}
                isPasswordToggle
                isPasswordVisible={showRegisterPassword}
                onAction={() => setShowRegisterPassword(!showRegisterPassword)}
              />
            </FloatingField>
            <FloatingField label="確認密碼">
              <AuthField
                icon="lock"
                value={confirmPassword}
                onChange={setConfirmPassword}
                placeholder="請再次輸入密碼"
                type={showRegisterPassword ? 'text' : 'password'}
                isPasswordToggle
                isPasswordVisible={showRegisterPassword}
                onAction={() => setShowRegisterPassword(!showRegisterPassword)}
              />
            </FloatingField>
            <FloatingField label="出生年次">
              <AuthField icon="calendar" value={birthYear} onChange={setBirthYear} placeholder="例如 1995 或 19950101" inputMode="numeric" />
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
              {submitting ? '寄送中' : '建立帳號'}
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
              <AuthField icon="code" value={registerCode} onChange={setRegisterCode} placeholder="6 位數驗證碼" inputMode="numeric" />
            </FloatingField>
            <div className="forgot-action-row">
              <button className="resend-button" type="button" disabled={submitting} onClick={submitRegister}>
                重新發送
              </button>
              <button className="primary-login-button compact-button" type="submit" disabled={submitting || registerCode.length < 6}>
                完成註冊
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
        <AuthField icon="mail" value={email} onChange={setEmail} placeholder="Email" inputMode="email" />
        <AuthField
          icon="lock"
          value={password}
          onChange={setPassword}
          placeholder="密碼"
          type={showPassword ? 'text' : 'password'}
          isPasswordToggle
          isPasswordVisible={showPassword}
          onAction={() => setShowPassword(!showPassword)}
        />
        <AuthNotice message={authMessage} error={authError} />
        <button className="primary-login-button" type="submit" disabled={submitting || email.trim().length === 0 || password.length === 0}>
          {submitting ? '登入中' : '登入'}
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
      <div className="login-status" aria-hidden="true">
        <strong>9:41</strong>
        <span>▮▮▮ ))) ▯</span>
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
  suffix,
  inputMode,
  isPasswordToggle = false,
  isPasswordVisible = false,
}: {
  icon: FieldIcon
  value: string
  onChange: (value: string) => void
  placeholder: string
  type?: string
  onAction?: () => void
  suffix?: string
  inputMode?: 'text' | 'email' | 'numeric'
  isPasswordToggle?: boolean
  isPasswordVisible?: boolean
}) {
  return (
    <label className="login-field">
      <span className="field-icon" aria-hidden="true">
        <FieldIconView icon={icon} />
      </span>
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        type={type}
        inputMode={inputMode}
      />
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
        <p className="legal-updated">最後更新：2026 年 5 月 29 日</p>
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
        <p className="legal-review-note">此為 MVP 商用前草案，正式上線、收費或 App 上架前，仍建議由台灣律師依公司主體、金流與實際資料流審閱。</p>
      </section>
    </div>
  )
}

const termsDocument = {
  title: '服務條款',
  sections: [
    {
      heading: '一、服務定位',
      items: [
        '同頻 Today 是生活共鳴型社交服務，協助使用者以今日事件、情緒標籤與低壓任務開始互動。本服務不保證配對、交友、約會或任何特定關係結果。',
        '使用者建立帳號、登入或使用本服務，即表示已閱讀並同意本條款與隱私權政策。'
      ]
    },
    {
      heading: '二、帳號與使用者責任',
      items: [
        '使用者應提供正確且可收信的電子郵件，並妥善保管密碼與驗證碼。不得冒用他人身分、使用他人信箱或以自動化方式大量建立帳號。',
        '使用者需自行確認分享內容的真實性與適當性，不得公開自己或他人的敏感個資、精準位置、電話、地址、身分證字號、車牌或可辨識第三人的資訊。'
      ]
    },
    {
      heading: '三、禁止行為',
      items: [
        '不得發表指名攻擊、仇恨言論、性騷擾、暴力威脅、鼓勵自傷、詐騙、借貸、投資或加密貨幣誘導、肉搜、洗版外部連結等內容。',
        '不得騷擾、跟蹤、恐嚇其他使用者，或將本服務用於徵才、廣告、商業招攬、非法交易或任何違反中華民國法律之行為。'
      ]
    },
    {
      heading: '四、內容與管理',
      items: [
        '使用者保留其發表內容的權利，但同意授權本服務在提供、維護、展示、備份、檢舉審查與安全防護範圍內使用該內容。',
        '若內容疑似違規或高風險，本服務得限制顯示、隱藏、刪除、暫停帳號或保留必要紀錄，以維護使用者安全與服務品質。'
      ]
    },
    {
      heading: '五、免責與責任限制',
      items: [
        '本服務以現況提供，可能因維護、第三方服務、網路或不可抗力而中斷或異常。使用者間互動、線下見面與任務參與所生風險，應由使用者自行審慎判斷。',
        '在法律允許範圍內，本服務不負擔間接、附帶、特殊、衍生性損害或資料遺失責任。'
      ]
    },
    {
      heading: '六、條款變更與準據法',
      items: [
        '本服務得因功能、法令或營運需求更新條款，重大變更將以站內公告或電子郵件通知。使用者於更新後繼續使用，視為同意更新內容。',
        '本條款以中華民國法律為準據法；如有爭議，雙方同意以台灣台北地方法院為第一審管轄法院，但消費者保護法另有規定者從其規定。'
      ]
    }
  ]
}

const privacyDocument = {
  title: '隱私權政策',
  sections: [
    {
      heading: '一、蒐集者與適用範圍',
      items: [
        '本政策適用於同頻 Today 提供的網站、PWA、API 與相關服務。正式商用前，應補上公司或營運主體名稱、統一編號、聯絡地址與客服信箱。',
        '我們依個人資料保護法告知使用者資料蒐集目的、類別、期間、地區、對象、方式，以及使用者可行使的權利。'
      ]
    },
    {
      heading: '二、蒐集資料類別',
      items: [
        '帳號資料：電子郵件、暱稱、密碼雜湊、出生年次、性別選項、條款同意時間、登入與驗證紀錄。',
        '服務內容資料：今日事件、情緒與興趣標籤、樹洞文章與留言、任務參與紀錄、聊天文字訊息、檢舉與安全審查紀錄。',
        '技術資料：IP 位址、瀏覽器或裝置資訊、操作時間、API 請求紀錄、錯誤紀錄與必要 Cookie 或本機儲存資料。'
      ]
    },
    {
      heading: '三、使用目的與方式',
      items: [
        '資料將用於帳號建立與登入、電子郵件驗證、找回密碼、今日共鳴配對、聊天室、任務參與、內容管理、違規防護、客服處理與服務改善。',
        '第一版不使用 OpenAI、Gemini、Claude 或其他生成式 AI API 分析使用者內容；目前標籤與配對為 rule-based 模擬。'
      ]
    },
    {
      heading: '四、保存期間、地區與分享對象',
      items: [
        '資料保存至帳號存在期間、使用目的完成、法令要求或爭議處理必要期間為止。逾期或無保存必要時，將刪除、停止使用或去識別化。',
        '資料可能儲存於台灣或雲端服務供應商所在地。必要時會提供給主機、資料庫、郵件寄送、安全維運等受託廠商，但不會任意出售個人資料。',
        '若依法令、法院、主管機關要求，或為防止詐騙、危害、重大違規與維護權益所必要，可能依法提供必要資料。'
      ]
    },
    {
      heading: '五、使用者權利',
      items: [
        '使用者可依個人資料保護法請求查詢或閱覽、製給複本、補充或更正、停止蒐集處理利用、刪除個人資料。',
        '若拒絕提供註冊或驗證必要資料，可能無法建立帳號、登入、使用配對、聊天或其他需要帳號的功能。'
      ]
    },
    {
      heading: '六、安全與未成年人',
      items: [
        '我們會採取合理安全措施，例如密碼雜湊、驗證碼期限、資料庫權限控管與內容風險檢查，但網際網路傳輸無法保證絕對安全。',
        '本服務不主動提供給未滿 13 歲者使用；若未成年人使用，應取得法定代理人同意並遵守所在地法令。'
      ]
    },
    {
      heading: '七、政策更新',
      items: [
        '本政策可能因功能、法令或實際資料流程調整而更新。重大變更會以站內公告或電子郵件通知，並於頁面標示最後更新日期。'
      ]
    }
  ]
}
