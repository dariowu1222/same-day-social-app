import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Heart, MessageCircle, Flag, HeartHandshake, X, ArrowUp } from 'lucide-react'
import ReportSheet from '../report/ReportSheet'
import { likeReply } from './api'
import type { RantPost } from './types'
import { flattenReplies, postImages } from './types'
import MediaInput, { type MediaState } from '../../shared/ui/MediaInput'
import PostImageGallery from './PostImageGallery'
import ReplyItem from './ReplyItem'
import PostMenu from './PostMenu'
import { COMPOSE_HINTS, QUICK_REPLIES, getHintSeenSet, markHintSeen, appendQuickReply, pickReplyPlaceholder } from './rantCompose'
import { MODE_LABELS } from './rantModes'

function avatarLetter(nickname: string) {
  return nickname.trim()[0]?.toUpperCase() ?? '?'
}

type Props = {
  post: RantPost
  replyText: string
  replyMedia: MediaState
  onReplyTextChange: (value: string) => void
  onReplyMediaChange: (media: MediaState) => void
  onUnderstand: () => void
  onReply: (parentReplyId?: string) => void
  // 檢舉改由卡片內的 ReportSheet 處理，外層只負責顯示結果訊息
  onReported?: (message: string) => void
  onDelete: () => void
  onLikedReply: () => void
  onHashtagClick?: (tag: string) => void
}

export default function RantPostCard({
  post, replyText, replyMedia,
  onReplyTextChange, onReplyMediaChange,
  onUnderstand, onReply, onReported, onDelete, onLikedReply, onHashtagClick,
}: Props) {
  const navigate = useNavigate()
  const [showReplies, setShowReplies] = useState(false)
  const [showReplyForm, setShowReplyForm] = useState(false)
  const [replyTarget, setReplyTarget] = useState<{ id: string; nickname: string } | null>(null)
  const [showHint, setShowHint] = useState(false)
  // 每次打開留言框抽一句當提示，打開之後就不再變（見 rantCompose）
  const [placeholder, setPlaceholder] = useState(() => pickReplyPlaceholder(post.mode))
  const [reportOpen, setReportOpen] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)
  const cardRef = useRef<HTMLElement>(null)
  const canReply = replyText.trim() || replyMedia.imageUrl || replyMedia.audioUrl

  const hintText = COMPOSE_HINTS[post.mode]
  const quickReplies = QUICK_REPLIES[post.mode] ?? []

  // 收合態要秀誰的頭像。整串都算（含子回覆），因為旁邊那個數字也是整串的
  // post.replyCount；同一個人回很多則只出現一次，不然會變成三顆一樣的圈。
  const replyPeople = [...new Set(flattenReplies(post.replies).map((r) => r.nickname))]

  // 手機版沒有「取消」按鈕，改成點卡片外面就收起來。
  //
  // 聽 click 而不是 pointerdown。pointerdown 試過，壞在兩件事：
  // 一、它在 click 之前。留言框一關，底下的卡片整串往上跳，手指放開時命中的
  //     已經是別的元素——實測點另一篇的「留下回應…」完全打不開（最糟是點到
  //     內文，直接跳去那篇的詳情頁）。改聽 click，是先讓該開的開、再關這邊，
  //     兩個 setState 落在同一輪，看不到中間狀態。
  // 二、手指按著往下滑也算 pointerdown，捲動看別篇會把打到一半的字收掉。
  //
  // 比對範圍是整張卡片而不是留言框：回覆底下那顆「回應」在留言框外面，
  // 以留言框為界的話，點它等於先關再開，白閃一下。
  //
  // 桌面維持明確的「取消」：滑鼠點旁邊的機率比手指高太多，誤觸代價不對等。
  // 判斷寫在 handler 裡而不是 effect 開頭，中途改變視窗寬度也還是對的。
  //
  // 草稿不會掉：replyText / replyMedia 由列表頁以 postId 存著，重開就在。
  //
  // 已知限制：iOS Safari 對非互動元素的 click 不一定會冒泡到 document。
  // 真要上 iOS，給外層容器加 cursor: pointer 就會補上，這裡先不預先加。
  useEffect(() => {
    if (!showReplyForm) return

    function handleOutsideClick(e: MouseEvent) {
      if (window.matchMedia('(min-width: 1024px)').matches) return
      if (cardRef.current?.contains(e.target as Node)) return
      setShowReplyForm(false)
      setReplyTarget(null)
    }

    // 隔一個 setTimeout 才掛，不是在裝可愛。
    //
    // 打開留言框的那一下 click 還在往上冒泡的時候，React 已經把更新沖出去、
    // 連帶把這個 effect 跑完了（discrete event 之後的 microtask checkpoint）。
    // 直接掛的話，同一個 click 接著就打到自己身上——而且必中：被點的
    // 「留下回應…」已經被留言框換掉，那個節點脫離 DOM，contains() 一定是 false。
    // 結果就是點一下開、同一下關，畫面上看起來像沒反應。
    //
    // setTimeout 是 macrotask，排在整個事件派發之後，這一輪就碰不到它了。
    const timer = setTimeout(() => document.addEventListener('click', handleOutsideClick), 0)
    return () => {
      clearTimeout(timer)
      document.removeEventListener('click', handleOutsideClick)
    }
  }, [showReplyForm])

  // 首次點開留言框 → 淡入情境提示，並標記該貼文已顯示（與詳情頁共用同一份 postId 記憶）
  useEffect(() => {
    if (!showReplyForm || !hintText) return
    if (getHintSeenSet().has(post.id)) return
    setShowHint(true)
    markHintSeen(post.id)
  }, [showReplyForm, hintText, post.id])

  function applyQuickReply(text: string) {
    onReplyTextChange(appendQuickReply(replyText, text))
    setTimeout(() => {
      const el = inputRef.current
      if (el) { el.focus(); const n = el.value.length; el.setSelectionRange(n, n) }
    }, 0)
  }

  function handleReply() {
    onReply(replyTarget?.id)
    setShowReplyForm(false)
    setReplyTarget(null)
    setShowHint(false)
  }

  function openReplyToPost() {
    setReplyTarget(null)
    setPlaceholder((prev) => pickReplyPlaceholder(post.mode, prev))
    setShowReplyForm(true)
    // 原本無條件展開。一則回覆都沒有的貼文也會開出一塊空的回覆區——
    // 一條分隔線加一顆「收起回應」，底下什麼都沒有。那就是兩條線的來源。
    if (post.replies.length > 0) setShowReplies(true)
  }

  function openReplyToReply(replyId: string, nickname: string) {
    setReplyTarget({ id: replyId, nickname })
    setPlaceholder((prev) => pickReplyPlaceholder(post.mode, prev))
    setShowReplyForm(true)
    setShowReplies(true)
  }

  return (
    <article ref={cardRef} className="card" style={{ gap: 0, padding: '14px 14px 8px' }}>
      {/* Thread row: 頭像(左) + 內容(右) */}
      <div className="thread-row">
        {/* 左：頭像 + 串文線 */}
        <div className="thread-left">
          <div className="avatar-circle" style={{ cursor: 'pointer', flexShrink: 0 }} onClick={() => navigate(`/rant/${post.id}`)}>
            {avatarLetter(post.nickname)}
          </div>
          {showReplies && post.replies.length > 0 && <div className="thread-line" />}
        </div>

        {/* 右：所有內容 */}
        <div className="thread-right">
          {/* 標題列（點擊進詳細頁）*/}
          <div
            className="thread-post-header"
            onClick={() => navigate(`/rant/${post.id}`)}
          >
            <div className="thread-post-header-info">
              <span className="post-author-name">{post.nickname}</span>
              <span className="post-time">
                {new Date(post.createdAt).toLocaleDateString('zh-TW', { month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
              </span>
              <span className="tag" data-mode={post.mode}>{MODE_LABELS[post.mode] ?? post.mode}</span>
            </div>
            <div onClick={(e) => e.stopPropagation()}>
              <PostMenu postId={post.id} isOwner={post.isMine} onDelete={onDelete} />
            </div>
          </div>

          {/* 內文（點擊進詳細頁）*/}
          <div onClick={() => navigate(`/rant/${post.id}`)} style={{ cursor: 'pointer' }}>
            <p className="post-content" style={{ marginBottom: 8 }}>{post.content}</p>
            <PostImageGallery images={postImages(post)} />
            {post.audioUrl && <audio controls src={post.audioUrl} className="post-media-audio" />}
          </div>

          {/* Tags */}
          {post.hashtags?.length > 0 && (
            <div className="hashtag-row" onClick={(e) => e.stopPropagation()}>
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
          {/* 動作列 */}
          <div className="thread-action-bar">
            <button className="post-action-btn" onClick={onUnderstand}>
              <Heart size={16} /> {post.likeCount > 0 ? post.likeCount : ''}
            </button>
            {/* 💬 現在一律是「留言」。
                底下那顆假輸入框「留下回應…」拿掉了——它和下面的「查看 N 則回應」
                是兩列長得很像的次要列疊在一起，中間還夾一條分隔線。
                留言的入口收回這顆 icon，展開交給那一列，一件事一個控制項。

                收合只給桌面（.thread-replies-collapse，見 17-desktop.css）：
                手機以滑動為主，展開之後再冒出一顆收合鈕只是多一列要跨過的東西。
                代價是手機上展開了就一直開著，直到重新載入列表——可以接受，
                真要細讀本來就會點進詳情頁。 */}
            <button
              className={`post-action-btn${showReplyForm ? ' is-active' : ''}`}
              aria-label="留下回應"
              onClick={(e) => { e.stopPropagation(); openReplyToPost() }}
            >
              <MessageCircle size={16} fill={showReplyForm ? 'currentColor' : 'none'} /> {post.replyCount > 0 ? post.replyCount : ''}
            </button>
            <button className="post-action-btn report-btn" onClick={() => setReportOpen(true)}>
              <Flag size={14} /> 檢舉
            </button>
          </div>

          {/* 有人回應了，但還沒展開。
              原本這件事只靠 💬 旁邊一個數字表示，於是整面牆上「有人回應的貼文」
              和「沒人理的貼文」長得幾乎一樣——而「有人懂你」正是這個功能的全部重點。
              數字是抽象的，幾張臉不是。

              樣式直接用子回覆那一層的 thread-sub-indicator：同一件事在這個 App 裡
              本來就長這樣（ReplyItem 的「查看 N 則回應」），往上搬一層只是讓它一致。

              刻意不預覽內容：樹洞的內容是重的，一面牆連續攤開十幾段別人的心事
              會讓人很快過載，而且一篇熱門貼文會把下面的人整個埋掉。
              「可以掃過去、想進去才進去」這個梯度要留著。 */}
          {post.replies.length > 0 && (
            <button
              type="button"
              className={`thread-sub-indicator${showReplies ? ' thread-replies-collapse' : ''}`}
              aria-expanded={showReplies}
              onClick={(e) => { e.stopPropagation(); setShowReplies((v) => !v) }}
            >
              {/* 展開後頭像就收起來——底下每一則自己都帶著臉，再堆一次是多的 */}
              {!showReplies && (
                <span className="thread-sub-avatars">
                  {replyPeople.slice(0, 3).map((nickname) => (
                    <span key={nickname} className="thread-sub-avatar-xs">{avatarLetter(nickname)}</span>
                  ))}
                </span>
              )}
              <span className="thread-sub-count">
                {showReplies ? '收起回應' : `查看 ${post.replyCount} 則回應`}
              </span>
            </button>
          )}
        </div>
      </div>

      {/* 回覆輸入區。入口是動作列的 💬（沒有回覆的貼文也一樣），
          所以收合態不再畫任何東西——以前那顆假輸入框「留下回應…」拿掉了。 */}
      {showReplyForm && (
        <div className="reply-reveal">
          <div className="reply-form" style={{ marginTop: 6 }}>
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

            {/* 照片／錄音 icon、輸入框、送出排成一列。手機只留一顆送出 icon
                （取消＝點卡片外面），桌面維持文字按鈕。兩種都畫出來，用 CSS 切，
                不必在 JS 裡量視窗寬度。 */}
            <div className="reply-form-bar">
              <MediaInput value={replyMedia} onChange={onReplyMediaChange} />
              <input
                ref={inputRef}
                className="reply-field"
                value={replyText}
                onChange={(e) => onReplyTextChange(e.target.value)}
                placeholder={replyTarget ? `回覆 @${replyTarget.nickname}…` : placeholder}
                autoFocus
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
                <button className="ghost" onClick={() => { setShowReplyForm(false); setReplyTarget(null) }}>取消</button>
                <button className="secondary" onClick={handleReply} disabled={!canReply}>送出</button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* 回覆列表。開合全交給上面的 💬，所以這裡既沒有「查看 N 則回應」
          也沒有「收起回應」——少掉兩列按鈕，也少掉一條分隔線。 */}
      {showReplies && post.replies.length > 0 && (
        <div className="thread-reply-section">
          {post.replies.map((reply) => (
            <ReplyItem
              key={reply.id}
              reply={reply}
              onReply={openReplyToReply}
              onLike={(replyId) => likeReply(post.id, replyId).then(onLikedReply)}
            />
          ))}
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
