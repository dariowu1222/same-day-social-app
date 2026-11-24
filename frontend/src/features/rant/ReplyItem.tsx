import { useState, type CSSProperties } from 'react'
import ImageLightbox from '../../shared/ui/ImageLightbox'
import ReportSheet from '../report/ReportSheet'
import { timeAgo } from '../../shared/lib/timeAgo'
import { flattenReplies, type RantReply } from './types'
import { avatarLetter, avatarTone } from './rantAvatar'
import HeartButton from './HeartButton'

// 回應裡的回應先直接給幾則，其餘就地「再看 N 則」
const SUBS_AT_FIRST = 2

// 回應的檢舉入口。原本只有貼文能檢舉，回應區反而是言語衝突最常發生的地方。
function ReplyReportButton({ replyId }: { replyId: string }) {
  const [open, setOpen] = useState(false)
  return (
    <>
      {/* 字留著（「檢舉」兩個字比一面灰旗子好認——會想按的人通常正在不舒服），
          顏色拿掉。回應裡的回應現在直接攤開，每一則都是紅字的話，一串對話
          讀起來像一排警告。 */}
      <button className="rr-act" type="button" onClick={() => setOpen(true)}>檢舉</button>
      {open && (
        <ReportSheet
          targetType="RANT_REPLY"
          targetId={replyId}
          title="檢舉這則回應"
          onClose={() => setOpen(false)}
        />
      )}
    </>
  )
}

// 回應圖片：列表顯示縮圖（沒有縮圖的舊資料回退原圖），點擊開全螢幕檢視原圖。
function ReplyImage({ reply }: { reply: RantReply }) {
  const [open, setOpen] = useState(false)
  if (!reply.imageUrl) return null
  return (
    <>
      <button type="button" className="reply-media-btn" onClick={() => setOpen(true)}>
        <img src={reply.thumbUrl || reply.imageUrl} className="reply-media-img" alt="" loading="lazy" />
      </button>
      {open && (
        <ImageLightbox images={[reply.imageUrl]} startIndex={0} onClose={() => setOpen(false)} />
      )}
    </>
  )
}

/**
 * 收回自己的回應。
 *
 * 分兩下：第一下只是把「收回」換成「確定收回？」，第二下才真的送。
 * 這顆字很小、又緊貼著愛心和「回應」，手機上滑動時很容易誤觸；收回是刪除，
 * 刪了就沒有了。不用彈窗是因為彈窗會把整個畫面蓋掉，為了一則回應不值得。
 *
 * 後端只讓「底下還沒有人接話」的回應被收回，所以有人接話的那幾則根本不畫這顆
 * （見下面的 canTakeBack）。萬一畫面是舊的、按下去才發現已經有人接話，
 * 後端的訊息會直接顯示在這裡。
 */
function TakeBackButton({ onConfirm }: { onConfirm: () => Promise<unknown> | void }) {
  const [asking, setAsking] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  if (error) return <span className="rr-note" role="status">{error}</span>

  if (!asking) {
    return <button type="button" className="rr-act rr-takeback" onClick={() => setAsking(true)}>收回</button>
  }

  async function confirm() {
    setBusy(true)
    try {
      await onConfirm()
    } catch (e) {
      setError(e instanceof Error ? e.message : '收回失敗，請稍後再試')
    } finally {
      setBusy(false)
      setAsking(false)
    }
  }

  return (
    <span className="rr-confirm">
      確定收回？
      <button type="button" className="rr-act rr-takeback is-sure" disabled={busy} onClick={confirm}>收回</button>
      <button type="button" className="rr-act" disabled={busy} onClick={() => setAsking(false)}>取消</button>
    </span>
  )
}

type Props = {
  reply: RantReply
  onReply: (replyId: string, nickname: string) => void
  /** like = true 是按上去，false 是收回 */
  onLike: (replyId: string, like: boolean) => Promise<unknown> | void
  /** 有給才會在自己的回應上出現「收回」 */
  onDelete?: (replyId: string) => Promise<unknown> | void
  /** 展開時一則接一則滑出來用的順序（0 起算）。不給就不做進場動畫。 */
  enterIndex?: number
}

export default function ReplyItem({ reply, onReply, onLike, onDelete, enterIndex }: Props) {
  // 後端已經把子孫攤平成一層（見 RantReplyTreeTests），這裡再攤一次只是保險
  const subReplies = flattenReplies(reply.replies ?? [])
  const [showAllSubs, setShowAllSubs] = useState(false)
  const shownSubs = showAllSubs ? subReplies : subReplies.slice(0, SUBS_AT_FIRST)
  const hiddenSubs = subReplies.length - shownSubs.length

  // 建立 id→reply 查找 map，用於顯示引用框
  const replyMap = new Map<string, RantReply>()
  replyMap.set(reply.id, reply)
  subReplies.forEach((s) => replyMap.set(s.id, s))

  // 若子回覆的 parentReplyId 不是第一層 reply，代表是回覆另一則子回覆，需要引用框
  function getQuoteParent(sub: RantReply): RantReply | null {
    if (!sub.parentReplyId || sub.parentReplyId === reply.id) return null
    return replyMap.get(sub.parentReplyId) ?? null
  }

  // 只有「自己的、而且底下還沒有人接話」的才收得回來（規則在後端 RantService.DeleteReply）
  function canTakeBack(target: RantReply): boolean {
    if (!onDelete || !target.isMine) return false
    return target.id === reply.id
      ? subReplies.length === 0
      : !subReplies.some((s) => s.parentReplyId === target.id)
  }

  function actions(target: RantReply) {
    return (
      <div className="rr-acts">
        <HeartButton
          className="rr-act rr-like"
          size={14}
          liked={!!target.isLiked}
          count={target.likeCount}
          onToggle={(like) => onLike(target.id, like)}
        />
        <button type="button" className="rr-act" onClick={() => onReply(target.id, target.nickname)}>回應</button>
        {canTakeBack(target)
          ? <TakeBackButton onConfirm={() => onDelete!(target.id)} />
          : !target.isMine && <ReplyReportButton replyId={target.id} />}
      </div>
    )
  }

  const enterStyle = enterIndex === undefined
    ? undefined
    : ({ ['--enter' as string]: Math.min(enterIndex, 5) } as CSSProperties)

  return (
    <div className={`rr${enterIndex === undefined ? '' : ' rr-enter'}`} style={enterStyle}>
      <span className={`rr-avatar tone-${avatarTone(reply.nickname)}`} aria-hidden="true">
        {avatarLetter(reply.nickname)}
      </span>

      <div className="rr-main">
        <div className="rr-head">
          <span className="rr-name">{reply.nickname}</span>
          <span className="rr-time">{timeAgo(reply.createdAt)}</span>
        </div>

        <p className="rr-text">{reply.content}</p>
        <ReplyImage reply={reply} />
        {reply.audioUrl && <audio controls src={reply.audioUrl} className="post-media-audio" />}
        {actions(reply)}

        {/* 回應裡的回應：直接縮排顯示在這一則底下。
            以前是折起來的（疊頭像＋「查看 N 則回應」，要再點一次），等於把
            「有人回了你、你又回了他」這段對話藏了兩層——而那正是樹洞最有溫度的部分。
            先給前幾則，多的就地往下長，不跳頁。 */}
        {shownSubs.length > 0 && (
          <div className="rr-subs">
            {shownSubs.map((sub) => {
              const quoteParent = getQuoteParent(sub)
              return (
                <div key={sub.id} className="rr rr-sub">
                  <span className={`rr-avatar tone-${avatarTone(sub.nickname)}`} aria-hidden="true">
                    {avatarLetter(sub.nickname)}
                  </span>
                  <div className="rr-main">
                    <div className="rr-head">
                      <span className="rr-name">{sub.nickname}</span>
                      <span className="rr-time">{timeAgo(sub.createdAt)}</span>
                    </div>

                    {/* 引用框：只在回覆子回覆時顯示 */}
                    {quoteParent && (
                      <div className="quote-box">
                        <span className="quote-author">{quoteParent.nickname}：</span>
                        <span className="quote-text">{quoteParent.content}</span>
                      </div>
                    )}

                    <p className="rr-text">{sub.content}</p>
                    <ReplyImage reply={sub} />
                    {sub.audioUrl && <audio controls src={sub.audioUrl} className="post-media-audio" />}
                    {actions(sub)}
                  </div>
                </div>
              )
            })}

            {hiddenSubs > 0 && (
              <button type="button" className="rr-more" onClick={() => setShowAllSubs(true)}>
                再看 {hiddenSubs} 則
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
