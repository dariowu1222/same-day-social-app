import type { ReactNode } from 'react'
import { CalendarDays, Eye, EyeOff, LockKeyhole, Mail, ShieldCheck, UserRound } from 'lucide-react'
import { getLegalContent, type LegalDoc } from './legalDocuments'

// 登入 / 註冊 / 忘記密碼三個畫面共用的展示元件，與 LoginPage 的狀態邏輯分離。

export type ForgotStep = 'account' | 'verify' | 'reset'

export function getForgotDescription(step: ForgotStep) {
  if (step === 'account') {
    return '輸入你的 Email，我們會寄送驗證碼協助你重設密碼'
  }
  if (step === 'verify') {
    return '輸入收到的驗證碼，確認這是你的帳號'
  }
  return '設定新密碼後，就可以重新登入'
}

export function AuthFrame({ children, variant, onBack, center = false }: { children: ReactNode; variant: 'warm' | 'clean'; onBack?: () => void; center?: boolean }) {
  return (
    <main className={`login-screen ${variant === 'clean' ? 'register-screen' : ''}`}>
      {onBack && (
        <button className="back-button" type="button" onClick={onBack} aria-label="返回">
          ←
        </button>
      )}
      <section className={`login-content${center ? ' login-content--center' : ''}`}>
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

export function LogoBlock({ compact = false }: { compact?: boolean }) {
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

export type FieldIcon = 'mail' | 'user' | 'lock' | 'code' | 'calendar'

export function AuthField({
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

export function FloatingField({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="floating-field">
      <span>{label}</span>
      {children}
    </div>
  )
}

export function PasswordStrength({ password }: { password: string }) {
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

export function AuthNotice({ message, error }: { message: string; error: string }) {
  if (!message && !error) {
    return null
  }
  return <p className={`auth-notice ${error ? 'error' : ''}`}>{error || message}</p>
}

export function LegalModal({ type, onClose }: { type: LegalDoc; onClose: () => void }) {
  const doc = getLegalContent(type)

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
