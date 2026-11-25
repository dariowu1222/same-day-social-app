import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'

type Props = {
  title: string
  description?: string
  /** 確定那顆的字：直接寫動作（「收回」「刪除」），不要寫「確定」 */
  confirmLabel: string
  /** 會造成損失的動作：確定那顆是紅字 */
  danger?: boolean
  /**
   * 按下確定。回傳 Promise 的話，等它的期間兩顆都不能按；成功就關掉。
   * 失敗（reject）不關，把錯誤訊息顯示在視窗裡——例如收回時後端說
   * 「已經有人回應這一則了」，使用者要看得到為什麼沒成功。
   */
  onConfirm: () => Promise<unknown> | void
  onClose: () => void
}

/**
 * 「真的要這樣做嗎」的確認視窗。收回回應、刪除貼文用它。
 *
 * 樣子參考 Threads：置中一張小卡，標題是問句、下面一行說後果、兩顆並排的按鈕。
 * 以前這兩個動作是「同一顆按鈕按兩下」——字很小、又夾在別的按鈕中間，
 * 第一次用的人不知道為什麼字變了，也容易連點兩下就刪掉。
 *
 * 畫在 body 底下（portal）：卡片的回應區有 overflow: clip，視窗不能長在裡面。
 * 點旁邊、按 Esc 都是取消。
 */
export default function ConfirmDialog({ title, description, confirmLabel, danger = false, onConfirm, onClose }: Props) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [onClose])

  async function confirm() {
    setBusy(true)
    setError('')
    try {
      await onConfirm()
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : '沒有成功，請稍後再試')
      setBusy(false)
    }
  }

  return createPortal(
    <div className="confirm-mask" onClick={busy ? undefined : onClose}>
      <div
        className="confirm-dialog"
        role="alertdialog"
        aria-modal="true"
        aria-label={title}
        onClick={(event) => event.stopPropagation()}
      >
        <div className="confirm-body">
          <p className="confirm-title">{title}</p>
          {description && <p className="confirm-desc">{description}</p>}
          {error && <p className="confirm-error" role="alert">{error}</p>}
        </div>
        <div className="confirm-actions">
          <button type="button" className="confirm-btn" disabled={busy} onClick={onClose}>取消</button>
          <button
            type="button"
            className={`confirm-btn is-primary${danger ? ' is-danger' : ''}`}
            disabled={busy}
            onClick={confirm}
          >
            {busy ? '處理中' : confirmLabel}
          </button>
        </div>
      </div>
    </div>,
    document.body,
  )
}
