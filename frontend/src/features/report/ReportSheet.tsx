// 檢舉面板：樹洞貼文／回應、聊天訊息、使用者共用。
// 選到「擔心對方想傷害自己」時，整個面板會換成關懷的語氣並列出求助資源——
// 那不是舉發違規，是替人求救，用同一套「檢舉」措辭會讓想幫忙的人猶豫。
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { X } from 'lucide-react'
import { submitReport } from './api'
import { REPORT_OPTIONS, SUPPORT_LINES, type ReportCategory, type ReportTargetType } from './types'

/**
 * 一定要掛到 body 底下，不能留在呼叫它的那張卡片裡。
 *
 * 這個面板本來就是 position: fixed 的遮罩，淺色模式也一直是正常的浮層。
 * 壞的是夜間：[data-theme="dark"] .rant-page .card 有 backdrop-filter: blur(8px)
 * （深色玻璃卡片），而那一行同時做了兩件事——
 *
 *   1. 替 fixed 的子孫建立 containing block。遮罩於是以「卡片」為準而不是視窗，
 *      實測 375×812：mask 變成 y124/h416、sheet 跑到 y-143。畫面上看起來就像
 *      面板在原頁面裡直接展開，而不是蓋上來。
 *   2. 建立 stacking context。z-index: 90 只在卡片內部有效，所以卡片外面的
 *      搜尋列（z 12）和底部導覽（z 20）反而畫在面板上面。
 *
 * 兩個症狀同一個根因，而且只要上游任何人在祖先加了 filter / transform /
 * backdrop-filter 就會再發生一次。搬到 body 底下就與上游無關了。
 * 做法與 ImageLightbox 一致（這個 App 既有的浮層寫法）。
 */
const portal = (node: ReactNode) => createPortal(node, document.body)

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
  // 成功之後不解除：面板會換成完成畫面，不該再有第二發
  const sendingRef = useRef(false)

  const isSelfHarm = category === 'SELF_HARM'

  // Esc 關閉 + 鎖背景捲動，跟 ImageLightbox 同一套（那是這個 App 既有的浮層做法）
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKey)
    const prevOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = prevOverflow
    }
  }, [onClose])

  async function send() {
    if (!category) return
    // sending 是 state，setState 要等下一次 render 才看得到。同一輪事件裡連點兩下
    // （手機上很容易）兩發都會通過檢查，第二發撞上資料庫的唯一索引。
    // 後端已經把那種情況視為「已檢舉過」而不是錯誤，但請求本身就不該送出去。
    if (sendingRef.current) return
    sendingRef.current = true
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
      sendingRef.current = false
    }
  }

  if (doneMessage) {
    return portal(
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

  return portal(
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
