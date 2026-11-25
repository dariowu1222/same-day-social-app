import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { MessageCircle, HeartHandshake, X, ArrowUp, ChevronDown } from 'lucide-react'
import ReportSheet from '../report/ReportSheet'
import type { RantPost } from './types'
import { flattenReplies, postImages } from './types'
import MediaInput, { type MediaState } from '../../shared/ui/MediaInput'
import PostImageGallery from './PostImageGallery'
import ReplyItem from './ReplyItem'
import ReplySortMenu from './ReplySortMenu'
import { sortReplies, useReplySort } from './replySort'
import PostMenu from './PostMenu'
import HeartButton from './HeartButton'
import { COMPOSE_HINTS, QUICK_REPLIES, getHintSeenSet, markHintSeen, appendQuickReply, pickReplyPlaceholder } from './rantCompose'
import { MODE_LABELS } from './rantModes'
import { avatarLetter, avatarTone } from './rantAvatar'
import { timeAgo } from '../../shared/lib/timeAgo'

// 展開後先給幾則第一層回應，其餘就地「再看 N 則」。
// 不一次全攤開：一篇熱門貼文有五十則的話，底下的人會被整個埋掉。
const REPLIES_AT_FIRST = 3
const REPLIES_PER_MORE = 5

// 收合動畫的長度，要跟 15-rant-card.css 的 rc-close 一致
const CLOSE_MS = 180

// 把留言框捲進畫面。
//
// 游標進留言框時瀏覽器會自己捲一次，但它不知道底部導覽是 fixed 的 64px——
// 實測 375×812：捲完輸入框下緣在 772，導覽從 748 開始，正好被壓掉一截。
// 留言框排在回覆列表後面，貼文底下回覆一多就更容易落到畫面外。
function revealField(el: HTMLElement | null) {
  if (!el) return
  // 已經看得見就不要亂動畫面——沒必要的捲動比沒捲更惱人
  if (el.getBoundingClientRect().bottom <= window.innerHeight - 96) return
  el.scrollIntoView({
    block: 'center',
    behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
  })
}

type Props = {
  post: RantPost
  /**
   * 這一篇的回應現在的狀態。牆上的列表只帶則數與前三個名字，回應要等點開才抓：
   * loading 是正在抓、failed 是沒抓到、ready（預設）是 post.replies 已經是完整的。
   */
  repliesState?: 'ready' | 'loading' | 'failed'
  /** 沒抓到的時候再試一次 */
  onRetryReplies?: () => void
  /**
   * 手指（或滑鼠）一按下去就先去抓回應，不等放開。從按下到放開大約有一百毫秒，
   * 白白等掉很可惜——回應是點開才抓的，這段時間剛好拿來抓。
   */
  onPrefetchReplies?: () => void
  /** 這一篇上一個動作失敗的說明（例如隱藏沒有成功）。顯示在回應區最上面。 */
  actionError?: string
  /**
   * 這一則的回應區是不是開著。由列表頁管，不是卡片自己記——
   * 因為「開另一則，前一則自動收起」這件事只有列表頁知道。
   */
  open: boolean
  onToggle: (open: boolean) => void
  replyText: string
  replyMedia: MediaState
  onReplyTextChange: (value: string) => void
  onReplyMediaChange: (media: MediaState) => void
  // 下面四個都回傳 Promise、失敗要 reject：愛心靠它決定要不要退回空心，
  // 留言框靠它決定要不要清空、要不要顯示錯誤
  /** like = true 是按上去，false 是收回 */
  onUnderstand: (like: boolean) => Promise<unknown> | void
  onReply: (parentReplyId?: string) => Promise<unknown> | void
  onLikeReply: (replyId: string, like: boolean) => Promise<unknown> | void
  onDeleteReply: (replyId: string) => Promise<unknown> | void
  /** 隱藏／取消隱藏別人的回應。只有自己的貼文才會用到（hide = true 是隱藏） */
  onHideReply: (replyId: string, hide: boolean) => Promise<unknown> | void
  // 檢舉改由卡片內的 ReportSheet 處理，外層只負責顯示結果訊息
  onReported?: (message: string) => void
  onDelete: () => Promise<unknown> | void
  onHashtagClick?: (tag: string) => void
}

export default function RantPostCard({
  post, repliesState = 'ready', onRetryReplies, onPrefetchReplies, actionError,
  open, onToggle, replyText, replyMedia,
  onReplyTextChange, onReplyMediaChange,
  onUnderstand, onReply, onLikeReply, onDeleteReply, onHideReply, onReported, onDelete, onHashtagClick,
}: Props) {
  const navigate = useNavigate()
  // 自己按「回應」收起來的時候，回應區多留 CLOSE_MS 播收合動畫。
  // 被別則擠掉（列表頁把 open 改成 false）不走這裡，當場消失——見 RantBoardPage。
  const [closing, setClosing] = useState(false)
  const closeTimer = useRef<number | undefined>(undefined)
  // 這次是按「回應」打開的（要把游標放進留言框），還是按頭像那列打開的（只是想看）
  const [focusOnOpen, setFocusOnOpen] = useState(false)
  const [visibleCount, setVisibleCount] = useState(REPLIES_AT_FIRST)
  // 進場動畫從第幾則開始算順序：「再看 N 則」長出來的那幾則要從 0 起跳，
  // 不然第八則會等前七則的延遲都過完才出現
  const [enterBase, setEnterBase] = useState(0)
  const [replyTarget, setReplyTarget] = useState<{ id: string; nickname: string } | null>(null)
  const [showHint, setShowHint] = useState(false)
  // 每次打開回應區抽一句當提示，打開之後就不再變（見 rantCompose）
  const [placeholder, setPlaceholder] = useState(() => pickReplyPlaceholder(post.mode))
  const [sending, setSending] = useState(false)
  const [sendError, setSendError] = useState('')
  const [reportOpen, setReportOpen] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)
  const canReply = !sending && Boolean(replyText.trim() || replyMedia.imageUrl || replyMedia.audioUrl)

  const hintText = COMPOSE_HINTS[post.mode]
  const quickReplies = QUICK_REPLIES[post.mode] ?? []

  // 收合態要秀誰的頭像。整串都算（含子回覆），因為旁邊那個數字也是整串的
  // post.replyCount；同一個人回很多則只出現一次，不然會變成三顆一樣的圈。
  // 列表給的是摘要時（replyNames），名字由後端先挑好；回應載進來之後就自己算。
  const replyPeople = post.replyNames ?? [...new Set(flattenReplies(post.replies).map((r) => r.nickname))]

  // 先排序、再決定先露出前幾則：選了「愛心多的在前」，最多愛心的那幾則就該在最上面，
  // 而不是把照時間的前幾則拿來重排
  const replySort = useReplySort()
  const sortedReplies = sortReplies(post.replies, replySort)
  const shownReplies = sortedReplies.slice(0, visibleCount)
  const hiddenReplies = sortedReplies.length - shownReplies.length

  useEffect(() => () => window.clearTimeout(closeTimer.current), [])

  /**
   * 開合都在「回應」這一顆上：按一下展開，再按一下收起。
   *
   * 以前展開後另外有一列「收起回應」（只給桌面，手機刻意不給）。現在不分裝置，
   * 在哪裡打開就在哪裡關——同一顆按鈕、同一個位置，而且比一行小字好按。
   * 開著的時候按鈕變深色、箭頭翻上去，表示「再按一次會收起」。
   *
   * focus：按「回應」是想回話，游標直接進留言框；按頭像那列是想看，不要跳鍵盤。
   */
  function toggle(focus: boolean) {
    window.clearTimeout(closeTimer.current)
    if (open) {
      const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches
      if (!reduce) {
        setClosing(true)
        closeTimer.current = window.setTimeout(() => setClosing(false), CLOSE_MS)
      }
      onToggle(false)
      return
    }
    // 每次打開都從乾淨的狀態開始：上一次「正在回誰」、提示、錯誤訊息都不帶過來
    // （上一次可能是被別則擠掉的，那時候來不及清）。收到一半又按開的話，
    // 上面已經把收合的計時器取消了，這裡要把「正在收」一起放掉，不然回應區會
    // 一直卡在收合的樣子。打到一半的字不會掉：那是列表頁以 postId 存著的。
    setClosing(false)
    setReplyTarget(null)
    setSendError('')
    setShowHint(false)
    setFocusOnOpen(focus)
    setVisibleCount(REPLIES_AT_FIRST)
    setEnterBase(0)
    setPlaceholder((prev) => pickReplyPlaceholder(post.mode, prev))
    // 按「回應」是要開口了，情境提示這時候給；按頭像那列只是想看，不給
    if (focus) armHint()
    onToggle(true)
  }

  const revealInput = () => revealField(inputRef.current)

  // 首次要回這篇 → 淡入情境提示，並標記該貼文已顯示（與詳情頁共用同一份 postId 記憶）。
  // 只是展開來看不算：提示是寫給「正要開口」的人看的。
  function armHint() {
    if (!hintText || getHintSeenSet().has(post.id)) return
    setShowHint(true)
    markHintSeen(post.id)
  }

  // 按「回應」打開：等展開動畫跑完（280ms 的 grid-template-rows 0fr→1fr）再量位置，
  // 中途量到的是還沒長完的高度。
  useEffect(() => {
    if (!open || !focusOnOpen) return
    const timer = window.setTimeout(() => revealField(inputRef.current), 300)
    return () => window.clearTimeout(timer)
  }, [open, focusOnOpen])

  function applyQuickReply(text: string) {
    onReplyTextChange(appendQuickReply(replyText, text))
    setTimeout(() => {
      const el = inputRef.current
      if (el) { el.focus(); const n = el.value.length; el.setSelectionRange(n, n) }
    }, 0)
  }

  async function handleReply() {
    if (!canReply) return
    setSending(true)
    setSendError('')
    try {
      await onReply(replyTarget?.id)
      setReplyTarget(null)
      setShowHint(false)
      // 自己剛送出的那一則要看得到。它排在最後面，還沒展開的那幾則如果繼續收著，
      // 送出之後畫面上什麼都沒多，會以為沒送成功。
      setEnterBase(shownReplies.length)
      setVisibleCount(Number.MAX_SAFE_INTEGER)
      // 鍵盤收下去，讓出畫面給剛送出的那一則。回應區維持開著。
      inputRef.current?.blur()
    } catch (e) {
      setSendError(e instanceof Error ? e.message : '送出失敗，請稍後再試')
    } finally {
      setSending(false)
    }
  }

  function openReplyToReply(replyId: string, nickname: string) {
    setReplyTarget({ id: replyId, nickname })
    armHint()
    inputRef.current?.focus({ preventScroll: true })
    window.setTimeout(revealInput, 0)
  }

  function showMore() {
    setEnterBase(shownReplies.length)
    setVisibleCount((n) => n + REPLIES_PER_MORE)
  }

  const goDetail = () => navigate(`/rant/${post.id}`)
  const threadMounted = open || closing

  return (
    <article id={`rant-${post.id}`} className={`card rc${open ? ' is-open' : ''}`}>
      {/* 標題列（點擊進詳細頁）*/}
      <header className="rc-head">
        <button type="button" className={`rc-avatar tone-${avatarTone(post.nickname)}`} onClick={goDetail} aria-label="看這篇貼文">
          {avatarLetter(post.nickname)}
        </button>
        <div className="rc-who" onClick={goDetail}>
          <span className="rc-name">{post.nickname}</span>
          <span className="rc-time">{timeAgo(post.createdAt)}</span>
        </div>
        <span className="tag rc-mode" data-mode={post.mode}>{MODE_LABELS[post.mode] ?? post.mode}</span>
        <PostMenu postId={post.id} isOwner={post.isMine} onDelete={onDelete} onReport={() => setReportOpen(true)} />
      </header>

      {/* 內文（點擊進詳細頁）*/}
      <div className="rc-body" onClick={goDetail}>
        <p className="post-content">{post.content}</p>
        <PostImageGallery images={postImages(post)} />
        {post.audioUrl && <audio controls src={post.audioUrl} className="post-media-audio" />}
      </div>

      {/* Tags */}
      {post.hashtags?.length > 0 && (
        <div className="hashtag-row">
          {post.hashtags.map((tag) => (
            <span
              key={tag}
              className={`hashtag-tag${onHashtagClick ? ' hashtag-tag-clickable' : ''}`}
              onClick={onHashtagClick ? () => onHashtagClick(tag) : undefined}
            >
              #{tag}
            </span>
          ))}
        </div>
      )}

      {/* 動作列：愛心、回應。檢舉收進右上角的 ⋮（見 PostMenu）。 */}
      <div className="rc-actions">
        <HeartButton
          className="rc-pill"
          liked={!!post.isLiked}
          count={post.likeCount}
          onToggle={onUnderstand}
          label="我懂"
        />
        <button
          type="button"
          className="rc-pill rc-reply-toggle"
          aria-expanded={open}
          onPointerDown={open ? undefined : onPrefetchReplies}
          onClick={() => toggle(true)}
        >
          <MessageCircle size={16} fill={open ? 'currentColor' : 'none'} />
          <span>回應{post.replyCount > 0 ? ` ${post.replyCount}` : ''}</span>
          <ChevronDown size={14} strokeWidth={2.6} className="rc-chev" />
        </button>
      </div>

      {/* 有人回應了，但還沒展開。
          這件事不能只靠「回應」旁邊一個數字表示：整面牆上「有人回應的貼文」和
          「沒人理的貼文」會長得幾乎一樣——而「有人懂你」正是這個功能的全部重點。
          數字是抽象的，幾張臉不是。

          點它一樣是展開，只是不把游標放進留言框（想看，不是想回）。
          展開之後這一列就消失：底下每一則自己都帶著臉，收合也改在「回應」那顆上。

          刻意不預覽內容：樹洞的內容是重的，一面牆連續攤開十幾段別人的心事會讓人
          很快過載。「可以掃過去、想進去才進去」這個梯度要留著。 */}
      {!threadMounted && post.replyCount > 0 && (
        <button type="button" className="rc-faces" onPointerDown={onPrefetchReplies} onClick={() => toggle(false)}>
          <span className="rc-faces-stack">
            {replyPeople.slice(0, 3).map((nickname) => (
              <span key={nickname} className={`rc-face tone-${avatarTone(nickname)}`}>{avatarLetter(nickname)}</span>
            ))}
          </span>
          <span>查看 {post.replyCount} 則回應</span>
        </button>
      )}

      {/* 回應區：回覆列表 + 留言框，一起開、一起收。 */}
      {threadMounted && (
        <div className={`rc-thread${open ? '' : ' is-closing'}`}>
          <div className="rc-thread-inner">
            {actionError && <p className="rc-thread-note" role="alert">{actionError}</p>}
            {/* 回應還在路上。留言框不必等它——想回話的人可以先打字。
                這行字晚 0.35 秒才出現（寫在 CSS）：多數時候回應在那之前就到了，
                每次點開都閃一下「載入中」反而讓人覺得慢。 */}
            {repliesState === 'loading' && post.replies.length === 0 && (
              <p className="rc-thread-note is-pending" role="status">回應載入中</p>
            )}
            {repliesState === 'failed' && (
              <p className="rc-thread-note" role="alert">
                回應沒有載入成功
                {onRetryReplies && (
                  <button type="button" className="rc-thread-retry" onClick={onRetryReplies}>再試一次</button>
                )}
              </p>
            )}
            {/* 排序：只有一則的時候沒有東西可排，不畫 */}
            {post.replies.length > 1 && (
              <div className="rc-sort-row"><ReplySortMenu /></div>
            )}
            {shownReplies.length > 0 && (
              <div className="rc-replies">
                {shownReplies.map((reply, i) => (
                  <ReplyItem
                    key={reply.id}
                    reply={reply}
                    enterIndex={Math.max(0, i - enterBase)}
                    onReply={openReplyToReply}
                    onLike={onLikeReply}
                    onDelete={onDeleteReply}
                    onHide={post.isMine ? onHideReply : undefined}
                  />
                ))}
              </div>
            )}

            {/* 剩下的就地往下長，不跳去詳情頁。 */}
            {hiddenReplies > 0 && (
              <button type="button" className="rc-more" onClick={showMore}>
                {hiddenReplies > REPLIES_PER_MORE
                  ? `再看 ${REPLIES_PER_MORE} 則（還有 ${hiddenReplies} 則）`
                  : `再看 ${hiddenReplies} 則`}
              </button>
            )}

            {/* 留言框，排在回覆列表「後面」。
                對話由上往下讀，自己的那一則接在最後才對（IG 留言區就是這樣，
                靠上面那條「↩ 回覆 @某人」說明對象）。放在貼文和回覆中間的話，
                點最底下那則的「回應」，輸入框卻跳到最上面——手指在下面、框在上面，
                手機上鍵盤一彈還可能把它推出畫面。 */}
            <div className="reply-form rc-compose">
              {replyTarget && (
                <div className="reply-target-hint">↩ 回覆 @{replyTarget.nickname}
                  <button className="reply-target-clear" onClick={() => setReplyTarget(null)}>×</button>
                </div>
              )}

              {/* 情境提示（一次性，可收合） */}
              {showHint && hintText && (
                <div className="reply-nudge">
                  <HeartHandshake size={15} strokeWidth={1.8} className="reply-nudge-icon" />
                  <span className="reply-nudge-text">{hintText}</span>
                  <button className="reply-nudge-close" onClick={() => setShowHint(false)} aria-label="收合提示">
                    <X size={14} strokeWidth={2} />
                  </button>
                </div>
              )}

              {/* 快捷回覆橫條（常駐，橫滑） */}
              {quickReplies.length > 0 && (
                <div className="quick-reply-bar">
                  <div className="quick-reply-scroll">
                    {quickReplies.map((q) => (
                      <button key={q} type="button" className="quick-reply-chip" onClick={() => applyQuickReply(q)}>{q}</button>
                    ))}
                  </div>
                  <span className="quick-reply-fade" />
                </div>
              )}

              {/* 照片／錄音 icon、輸入框、送出排成一列。手機只留一顆送出 icon，
                  桌面是文字按鈕。兩種都畫出來，用 CSS 切，不必在 JS 裡量視窗寬度。
                  以前桌面還有一顆「取消」——現在收起來是按「回應」，不需要了。 */}
              <div className="reply-form-bar">
                <MediaInput value={replyMedia} onChange={onReplyMediaChange} />
                <input
                  ref={inputRef}
                  className="reply-field"
                  value={replyText}
                  onChange={(e) => onReplyTextChange(e.target.value)}
                  onFocus={armHint}
                  placeholder={replyTarget ? `回覆 @${replyTarget.nickname}…` : placeholder}
                  autoFocus={focusOnOpen}
                />
                <button
                  className="reply-send-btn reply-form-send"
                  onClick={handleReply}
                  disabled={!canReply}
                  aria-label="送出"
                >
                  <ArrowUp size={18} strokeWidth={2.2} />
                </button>
                <div className="reply-form-actions">
                  <button className="secondary" onClick={handleReply} disabled={!canReply}>送出</button>
                </div>
              </div>

              {sendError && <p className="rc-send-error" role="alert">{sendError}</p>}
            </div>
          </div>
        </div>
      )}

      {reportOpen && (
        <ReportSheet
          targetType="RANT_POST"
          targetId={post.id}
          onClose={() => setReportOpen(false)}
          onDone={(message) => onReported?.(message)}
        />
      )}
    </article>
  )
}
