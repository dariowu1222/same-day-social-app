import { useState, type CSSProperties, type ReactNode } from 'react'
import { Eye, EyeOff, Flag, Undo2 } from 'lucide-react'
import ImageLightbox from '../../shared/ui/ImageLightbox'
import ActionMenu, { type ActionMenuItem } from '../../shared/ui/ActionMenu'
import ConfirmDialog from '../../shared/ui/ConfirmDialog'
import ReportSheet from '../report/ReportSheet'
import { timeAgo } from '../../shared/lib/timeAgo'
import { flattenReplies, type RantReply } from './types'
import { avatarLetter, avatarTone } from './rantAvatar'
import HeartButton from './HeartButton'

// 回應裡的回應先直接給幾則，其餘就地「再看 N 則」
const SUBS_AT_FIRST = 2

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
 * 貼文作者看到的「我藏起來的那一則」。
 *
 * 預設收著：藏它就是不想再看到那句話，不該每次打開自己的貼文又被迫讀一次。
 * 但內容還在、點「看內容」就看得到——要檢舉的時候得確認自己檢舉的是哪一則。
 * 取消隱藏與檢舉在這一則右上角的「⋯」裡，跟其他回應的位置一樣。
 */
function HiddenReply({ children }: { children: ReactNode }) {
  const [shown, setShown] = useState(false)
  return (
    <div className="rr-hidden">
      <div className="rr-hidden-line">
        <p className="rr-hidden-label">
          <EyeOff size={13} strokeWidth={2.2} aria-hidden="true" />
          已隱藏，其他人看不到
        </p>
        <button type="button" className="rr-act" aria-expanded={shown} onClick={() => setShown((v) => !v)}>
          {shown ? '收起' : '看內容'}
        </button>
      </div>
      {shown && <div className="rr-hidden-body">{children}</div>}
    </div>
  )
}

type Props = {
  reply: RantReply
  /** 不給就不畫「回應」那顆——貼文已經被作者刪掉的時候，底下不能再接話 */
  onReply?: (replyId: string, nickname: string) => void
  /** like = true 是按上去，false 是收回 */
  onLike: (replyId: string, like: boolean) => Promise<unknown> | void
  /** 有給才會在自己的回應的選單裡出現「收回」 */
  onDelete?: (replyId: string) => Promise<unknown> | void
  /**
   * 有給＝這是我的貼文，別人的回應的選單裡會多一項「隱藏」。
   * hide = true 是隱藏，false 是取消隱藏。
   */
  onHide?: (replyId: string, hide: boolean) => Promise<unknown> | void
  /** 展開時一則接一則滑出來用的順序（0 起算）。不給就不做進場動畫。 */
  enterIndex?: number
}

/**
 * 一則回應，連同接在它底下的那幾則。
 *
 * 每一則底下只留兩顆最常按的：愛心、回應。其餘的——隱藏、收回、檢舉——收在
 * 右上角的「⋯」裡（參考 Threads）。以前那幾顆是跟愛心排在同一列的小字，
 * 貼文作者看自己的貼文時每一則都是四顆：一整排「隱藏」「檢舉」，像一面警告牆，
 * 而那是很少按的功能。
 */
export default function ReplyItem({ reply, onReply, onLike, onDelete, onHide, enterIndex }: Props) {
  // 後端已經把子孫攤平成一層（見 RantReplyTreeTests），這裡再攤一次只是保險
  const subReplies = flattenReplies(reply.replies ?? [])
  const [showAllSubs, setShowAllSubs] = useState(false)
  const shownSubs = showAllSubs ? subReplies : subReplies.slice(0, SUBS_AT_FIRST)
  const hiddenSubs = subReplies.length - shownSubs.length
  // 正在問「要不要收回」的是哪一則、正在檢舉的是哪一則。一次只會有一個視窗
  const [takingBack, setTakingBack] = useState<string | null>(null)
  const [reporting, setReporting] = useState<string | null>(null)

  // 建立 id→reply 查找 map，用於顯示引用框
  const replyMap = new Map<string, RantReply>()
  replyMap.set(reply.id, reply)
  subReplies.forEach((s) => replyMap.set(s.id, s))

  // 若子回覆的 parentReplyId 不是第一層 reply，代表是回覆另一則子回覆，需要引用框
  function getQuoteParent(sub: RantReply): RantReply | null {
    if (!sub.parentReplyId || sub.parentReplyId === reply.id) return null
    return replyMap.get(sub.parentReplyId) ?? null
  }

  // 只有「底下還沒有人接話」的才收得回來（規則在後端 RantService.DeleteReply）
  function hasAnswers(target: RantReply): boolean {
    return target.id === reply.id
      ? subReplies.length > 0
      : subReplies.some((s) => s.parentReplyId === target.id)
  }

  // 我親手藏的那一則：收合顯示。跟著上面那則一起被藏的（isHidden 但不能取消）
  // 照常攤開、只是淡一點——每一則都各自收合的話，要看一串得點好幾次。
  const collapsed = (target: RantReply) => !!target.isHidden && !!target.canUnhide

  /**
   * 這一則的「⋯」裡有什麼。看的是「這則是誰的」和「這篇是誰的」：
   *  ・我自己的回應：收回（底下有人接話就不能收，選項留著但按不了，並說明原因）
   *  ・別人的回應、而這是我的貼文：隱藏／取消隱藏
   *  ・別人的回應：檢舉
   * 沒有任何一項的時候 ActionMenu 自己不會畫那顆按鈕。
   */
  function menuItems(target: RantReply): ActionMenuItem[] {
    const items: ActionMenuItem[] = []

    if (target.isMine) {
      if (onDelete) {
        const blocked = hasAnswers(target)
        items.push({
          key: 'take-back',
          label: '收回',
          icon: <Undo2 size={16} />,
          danger: !blocked,
          disabled: blocked,
          hint: blocked ? '已經有人回應這一則，沒辦法收回' : undefined,
          onSelect: () => setTakingBack(target.id),
        })
      }
      return items
    }

    if (onHide) {
      if (target.isHidden && target.canUnhide) {
        items.push({
          key: 'unhide',
          label: '取消隱藏',
          icon: <Eye size={16} />,
          onSelect: () => { void onHide(target.id, false) },
        })
      } else if (!target.isHidden) {
        items.push({
          key: 'hide',
          label: '隱藏',
          hint: '其他人看不到，之後可以取消',
          icon: <EyeOff size={16} />,
          onSelect: () => { void onHide(target.id, true) },
        })
      }
    }

    items.push({
      key: 'report',
      label: '檢舉',
      icon: <Flag size={16} />,
      danger: true,
      onSelect: () => setReporting(target.id),
    })
    return items
  }

  function head(target: RantReply) {
    return (
      <div className="rr-head">
        <span className="rr-name">{target.nickname}</span>
        <span className="rr-time">{timeAgo(target.createdAt)}</span>
        <ActionMenu items={menuItems(target)} label="更多" className="rr-menu" />
      </div>
    )
  }

  function actions(target: RantReply) {
    if (target.isHidden) {
      return (
        <div className="rr-acts">
          <span className="rr-hidden-tag">跟著上面那則一起隱藏</span>
        </div>
      )
    }
    return (
      <div className="rr-acts">
        <HeartButton
          className="rr-act rr-like"
          size={14}
          liked={!!target.isLiked}
          count={target.likeCount}
          onToggle={(like) => onLike(target.id, like)}
        />
        {onReply && (
          <button type="button" className="rr-act" onClick={() => onReply(target.id, target.nickname)}>回應</button>
        )}
      </div>
    )
  }

  function content(target: RantReply, quoteParent: RantReply | null) {
    return (
      <>
        {/* 引用框：只在回覆子回覆時顯示 */}
        {quoteParent && (
          <div className="quote-box">
            <span className="quote-author">{quoteParent.nickname}：</span>
            <span className="quote-text">{quoteParent.content}</span>
          </div>
        )}
        <p className="rr-text">{target.content}</p>
        <ReplyImage reply={target} />
        {target.audioUrl && <audio controls src={target.audioUrl} className="post-media-audio" />}
      </>
    )
  }

  const enterStyle = enterIndex === undefined
    ? undefined
    : ({ ['--enter' as string]: Math.min(enterIndex, 5) } as CSSProperties)

  /* 回應裡的回應：直接縮排顯示在這一則底下。
     以前是折起來的（疊頭像＋「查看 N 則回應」，要再點一次），等於把
     「有人回了你、你又回了他」這段對話藏了兩層——而那正是樹洞最有溫度的部分。
     先給前幾則，多的就地往下長，不跳頁。 */
  const subs = shownSubs.length > 0 && (
    <div className="rr-subs">
      {shownSubs.map((sub) => {
        const quoteParent = getQuoteParent(sub)
        return (
          <div key={sub.id} className={`rr rr-sub${sub.isHidden ? ' is-hidden' : ''}`}>
            <span className={`rr-avatar tone-${avatarTone(sub.nickname)}`} aria-hidden="true">
              {avatarLetter(sub.nickname)}
            </span>
            <div className="rr-main">
              {head(sub)}

              {collapsed(sub) ? (
                <HiddenReply>{content(sub, quoteParent)}</HiddenReply>
              ) : (
                <>
                  {content(sub, quoteParent)}
                  {actions(sub)}
                </>
              )}
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
  )

  return (
    <div
      className={`rr${enterIndex === undefined ? '' : ' rr-enter'}${reply.isHidden ? ' is-hidden' : ''}`}
      style={enterStyle}
    >
      <span className={`rr-avatar tone-${avatarTone(reply.nickname)}`} aria-hidden="true">
        {avatarLetter(reply.nickname)}
      </span>

      <div className="rr-main">
        {head(reply)}

        {/* 第一層被藏的時候，接在它底下的那幾則也一起收進去：
            它們是跟著被藏的，攤在外面等於那句話還掛在畫面上。 */}
        {collapsed(reply) ? (
          <HiddenReply>
            {content(reply, null)}
            {subs}
          </HiddenReply>
        ) : (
          <>
            {content(reply, null)}
            {actions(reply)}
            {subs}
          </>
        )}
      </div>

      {/* 收回要先問一次：收回之後這一則就從所有人的畫面上消失，自己也拿不回來。
          後端不給收（剛好有人接話了）的時候，它的說明會直接顯示在這個視窗裡。 */}
      {takingBack && (
        <ConfirmDialog
          title="收回這則回應？"
          description="收回之後所有人都看不到，也拿不回來"
          confirmLabel="收回"
          danger
          onConfirm={() => onDelete?.(takingBack)}
          onClose={() => setTakingBack(null)}
        />
      )}

      {reporting && (
        <ReportSheet
          targetType="RANT_REPLY"
          targetId={reporting}
          title="檢舉這則回應"
          onClose={() => setReporting(null)}
        />
      )}
    </div>
  )
}
