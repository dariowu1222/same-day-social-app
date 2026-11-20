import { useState } from 'react'
import { AuthFrame, LogoBlock, AuthField, AuthNotice, FloatingField, LegalModal } from './AuthControls'
import type { LegalDoc } from './legalDocuments'
import type { ConsentAnswer } from './api'
import { isAdultBirthYear } from './ageRule'

/**
 * 「我已閱讀並同意服務條款與隱私權政策」那一列。
 * Email 註冊表單與確認畫面共用——同意的是同一份文件，
 * 兩邊的文字各寫各的話，遲早有一邊跟不上改版。
 */
export function TermsRow({ agreed, onChange, onOpen }: {
  agreed: boolean
  onChange: (agreed: boolean) => void
  onOpen: (doc: LegalDoc) => void
}) {
  return (
    <label className="terms-row">
      <input type="checkbox" checked={agreed} onChange={(event) => onChange(event.target.checked)} />
      <span>
        我已閱讀並同意{' '}
        <button type="button" onClick={() => onOpen('terms')}>服務條款</button>
        {' '}與{' '}
        <button type="button" onClick={() => onOpen('privacy')}>隱私權政策</button>
      </span>
    </label>
  )
}

type Props = {
  heading: string
  description: string
  /**
   * 使用者勾了同意、填了成年的年次之後呼叫。
   * 丟出來的 Error，message 會直接顯示在表單上（後端的拒絕理由走這條路回來）。
   */
  onSubmit: (answer: ConsentAnswer) => Promise<void>
  /** 不同意時的出口。登入頁是「返回」，登入後的補問是「登出」。 */
  exitPrompt: string
  exitLabel: string
  onExit: () => void
  /** 左上角要不要有返回箭頭。登入後的補問沒有「上一頁」可回，所以不給。 */
  onBack?: () => void
}

/**
 * 「同意條款＋確認成年」的確認畫面。
 *
 * 用在兩個地方，問的是同一件事：
 * · 第一次用 Google／Facebook 登入（建立帳號之前）
 * · 以前用社群登入建的帳號，登入後補問一次（那條路當時什麼都沒問）
 *
 * 勾選與年次的狀態收在這個元件裡，每次出現都是空的——
 * 同意必須是使用者對「這一次」按下的，不能沿用別的表單上勾過的那一格。
 */
export default function ConsentScreen({ heading, description, onSubmit, exitPrompt, exitLabel, onExit, onBack }: Props) {
  const [agreed, setAgreed] = useState(false)
  const [birthYear, setBirthYear] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [legalDoc, setLegalDoc] = useState<LegalDoc | null>(null)

  async function submit() {
    if (submitting) return

    // 三樣都先在這裡擋，不必跑一趟後端；後端有同樣的檢查，這裡只是省時間、訊息也比較準。
    if (!agreed) {
      setError('請先閱讀並同意服務條款與隱私權政策')
      return
    }
    if (birthYear.replace(/\D/g, '').length < 4) {
      setError('請輸入四位數的出生年份，例如 1995')
      return
    }
    if (!isAdultBirthYear(birthYear)) {
      setError('未滿 18 歲暫時不能使用同頻 Today')
      return
    }

    setSubmitting(true)
    setError('')
    try {
      await onSubmit({ termsAccepted: true, birthYear })
    } catch (failure) {
      setError(failure instanceof Error && failure.message ? failure.message : '操作失敗，請稍後再試')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthFrame variant="clean" onBack={onBack}>
      <LogoBlock compact />
      <section className="register-heading">
        <h1>{heading}</h1>
        <p>{description}</p>
      </section>

      <form
        className="register-form"
        onSubmit={(event) => {
          event.preventDefault()
          void submit()
        }}
      >
        <FloatingField label="出生年次">
          <AuthField
            icon="calendar"
            value={birthYear}
            onChange={(value) => { setError(''); setBirthYear(value) }}
            placeholder="出生年份，例如 1995"
            inputMode="numeric"
            autoComplete="bday-year"
          />
        </FloatingField>
        <p className="social-consent-note">同頻 Today 只提供年滿 18 歲的人使用</p>

        <TermsRow agreed={agreed} onChange={(next) => { setError(''); setAgreed(next) }} onOpen={setLegalDoc} />

        <AuthNotice message="" error={error} />

        <button className="register-submit" type="submit" disabled={submitting}>
          {submitting && <span className="btn-spinner" aria-hidden="true" />}{submitting ? '處理中' : '同意並繼續'}
        </button>
      </form>

      <p className="auth-switch">
        {exitPrompt}
        <button type="button" onClick={onExit}>
          {exitLabel}
        </button>
      </p>
      {legalDoc && <LegalModal type={legalDoc} onClose={() => setLegalDoc(null)} />}
    </AuthFrame>
  )
}
