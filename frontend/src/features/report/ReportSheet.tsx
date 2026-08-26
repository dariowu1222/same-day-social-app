// 檢舉面板：樹洞貼文／回應、聊天訊息、使用者共用。
// 選到「擔心對方想傷害自己」時，整個面板會換成關懷的語氣並列出求助資源——
// 那不是舉發違規，是替人求救，用同一套「檢舉」措辭會讓想幫忙的人猶豫。
import { useState } from 'react'
import { X } from 'lucide-react'
import { submitReport } from './api'
import { REPORT_OPTIONS, SUPPORT_LINES, type ReportCategory, type ReportTargetType } from './types'

type Props = {
  targetType: ReportTargetType
  targetId: string
  title?: string
  onClose: () => void
  onDone?: (message: string) => void
}

export default function ReportSheet({ targetType, targetId, title = '檢舉', onClose, onDone }: Props) {
  const [category, setCategory] = useState<ReportCategory | null>(null)
  const [detail, setDetail] = useState('')
  const [sending, setSending] = useState(false)
  const [error, setError] = useState('')
  // 送出後停在完成畫面，而不是直接關閉：
  // 自我傷害的回覆包含求助資源，用一閃即逝的 toast 會讓人來不及看。
  const [doneMessage, setDoneMessage] = useState<string | null>(null)

  const isSelfHarm = category === 'SELF_HARM'

  async function send() {
    if (!category || sending) return
    setSending(true)
    setError('')
    try {
      const res = await submitReport({ targetType, targetId, category, detail: detail.trim() || null })
      setDoneMessage(res.data.message)
      setSending(false)
      onDone?.(res.data.message)
    } catch {
      setError('送出失敗，請稍後再試。')
      setSending(false)
    }
  }

  if (doneMessage) {
    return (
      <div className="report-mask" onClick={onClose}>
        <div className="report-sheet" onClick={(e) => e.stopPropagation()} role="dialog" aria-label="已送出">
          <div className="report-body report-done">
            <p className="report-done-text">{doneMessage}</p>
            {isSelfHarm && (
              <div className="report-support">
                <span className="report-support-title">如果情況緊急，這些專線 24 小時都有人在：</span>
                {SUPPORT_LINES.map((line) => (
                  <a key={line.phone} className="report-support-line" href={`tel:${line.phone}`}>
                    {line.name} <strong>{line.phone}</strong>
                  </a>
                ))}
              </div>
            )}
          </div>
          <div className="report-actions">
            <button type="button" className="report-submit" onClick={onClose}>知道了</button>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="report-mask" onClick={onClose}>
      <div className="report-sheet" onClick={(e) => e.stopPropagation()} role="dialog" aria-label={title}>
        <div className="report-head">
          <span className="report-title">{isSelfHarm ? '謝謝你願意留意' : title}</span>
          <button type="button" className="report-close" onClick={onClose} aria-label="關閉">
            <X size={18} />
          </button>
        </div>

        <div className="report-body">
          <p className="report-lead">
            {isSelfHarm
              ? '我們會優先關心對方，也不會讓他知道是誰說的。'
              : '請選擇最接近的原因，這會幫助我們更快處理。'}
          </p>

          <div className="report-options">
            {REPORT_OPTIONS.map((opt) => (
              <button
                key={opt.value}
                type="button"
                className={`report-option${category === opt.value ? ' selected' : ''}`}
                onClick={() => setCategory(opt.value)}
              >
                <span className="report-option-label">{opt.label}</span>
                <span className="report-option-hint">{opt.hint}</span>
              </button>
            ))}
          </div>

          {/* 選到自我傷害時，先把求助資源放在眼前——比送出檢舉更要緊 */}
          {isSelfHarm && (
            <div className="report-support">
              <span className="report-support-title">如果情況緊急，這些專線 24 小時都有人在：</span>
              {SUPPORT_LINES.map((line) => (
                <a key={line.phone} className="report-support-line" href={`tel:${line.phone}`}>
                  {line.name} <strong>{line.phone}</strong>
                </a>
              ))}
            </div>
          )}

          {category && (
            <textarea
              className="report-detail"
              value={detail}
              onChange={(e) => setDetail(e.target.value)}
              maxLength={500}
              placeholder={isSelfHarm ? '想補充的狀況（選填）' : '補充說明（選填）'}
            />
          )}

          {error && <span className="report-error">{error}</span>}
        </div>

        <div className="report-actions">
          <button type="button" className="report-cancel" onClick={onClose}>取消</button>
          <button
            type="button"
            className="report-submit"
            onClick={send}
            disabled={!category || sending}
          >
            {sending ? '送出中…' : isSelfHarm ? '通知我們' : '送出檢舉'}
          </button>
        </div>
      </div>
    </div>
  )
}
