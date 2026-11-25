import { useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { MessageCircle, ChevronLeft, HeartHandshake, ArrowUp, X } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { deleteRant, deleteReply, getRant, hideReply, likeReply, replyRant, understandRant, unhideReply, unlikeRant, unlikeReply } from './api'
import ReportSheet from '../report/ReportSheet'
import type { RantPost } from './types'
import { postImages, withReplyHidden } from './types'
import MediaInput, { EMPTY_MEDIA, type MediaState } from '../../shared/ui/MediaInput'
import PostImageGallery from './PostImageGallery'
import ReplyItem from './ReplyItem'
import ReplySortMenu from './ReplySortMenu'
import { sortReplies, useReplySort } from './replySort'
import HeartButton from './HeartButton'
import { avatarLetter, avatarTone } from './rantAvatar'
import { timeAgo } from '../../shared/lib/timeAgo'
import PostMenu from './PostMenu'
import { COMPOSE_HINTS, QUICK_REPLIES, getHintSeenSet, markHintSeen, appendQuickReply, pickReplyPlaceholder, DEFAULT_REPLY_PLACEHOLDER } from './rantCompose'
import { MODE_LABELS } from './rantModes'

// 完整的日期時間。畫面上寫的是「18 分鐘前」，這個放在 title 裡，想知道確切時間的人看得到。
function formatTime(iso: string) {
  return new Date(iso).toLocaleDateString('zh-TW', {
    month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit',
  })
}

export default function RantDetailPage() {
  const { user } = useAuth()
  const { rantId } = useParams<{ rantId: string }>()
  const navigate = useNavigate()
  const [post, setPost] = useState<RantPost | null>(null)
  // 分辨「還在抓」與「抓完了但這篇不存在」，否則被刪的貼文會永遠停在「載入中…」
  const [loaded, setLoaded] = useState(false)
  const [replyText, setReplyText] = useState('')
  const [replyMedia, setReplyMedia] = useState<MediaState>(EMPTY_MEDIA)
  const [replyTarget, setReplyTarget] = useState<{ id: string; nickname: string } | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [composeFocused, setComposeFocused] = useState(false)
  const [showHint, setShowHint] = useState(false)
  const [reportOpen, setReportOpen] = useState(false)
  // 隱藏／取消隱藏失敗時的說明
  const [actionError, setActionError] = useState('')
  // 留言框的預設提示：進到這篇貼文時抽一次，之後就不動。
  // 列表頁是每次打開留言框抽，詳情頁的留言框一直開著，所以改成每篇抽一次。
  const [placeholder, setPlaceholder] = useState(DEFAULT_REPLY_PLACEHOLDER)
  const inputRef = useRef<HTMLInputElement>(null)
  const replySort = useReplySort()

  const hintText = post ? COMPOSE_HINTS[post.mode] : undefined
  const quickReplies = post ? (QUICK_REPLIES[post.mode] ?? []) : []

  useEffect(() => { loadPost() }, [rantId])

  // 只看 post?.id：送出回覆後 loadPost() 會換一個 post 物件，但還是同一篇，
  // 不該趁人剛送完就換一句。
  useEffect(() => {
    if (post) setPlaceholder((prev) => pickReplyPlaceholder(post.mode, prev))
  }, [post?.id])

  // 首次點開留言框 → 淡入情境提示，並標記該貼文已顯示（之後永不再跳）
  useEffect(() => {
    if (!composeFocused || !post || !hintText) return
    if (getHintSeenSet().has(post.id)) return
    setShowHint(true)
    markHintSeen(post.id)
  }, [composeFocused, post, hintText])

  // 快捷回覆：帶入輸入框可續編輯，不直接送出
  function applyQuickReply(text: string) {
    setReplyText(prev => appendQuickReply(prev, text))
    setComposeFocused(true)
    setTimeout(() => {
      const el = inputRef.current
      if (el) { el.focus(); const n = el.value.length; el.setSelectionRange(n, n) }
    }, 0)
  }

  async function loadPost() {
    if (!rantId) return
    try {
      const res = await getRant(rantId)
      setPost(res.data)
    } catch {
      // 找不到（已被刪除或下架）就讓畫面顯示空狀態，不要永遠停在「載入中…」
      setPost(null)
    } finally {
      setLoaded(true)
    }
  }

  async function handleUnderstand(like: boolean) {
    if (!rantId) return
    await (like ? understandRant(rantId) : unlikeRant(rantId))
    await loadPost()
  }

  async function handleReply() {
    if (!rantId || submitting || !user) return
    setSubmitting(true)
    try {
      await replyRant(rantId, {
        content: replyText,
        ...replyMedia,
        parentReplyId: replyTarget?.id ?? null,
      })
      setReplyText('')
      setReplyMedia(EMPTY_MEDIA)
      setReplyTarget(null)
      setComposeFocused(false)
      await loadPost()
    } finally {
      setSubmitting(false)
    }
  }

  // 隱藏／取消隱藏：畫面先變，再等後端；失敗就還原並說一聲（理由同 RantBoardPage）
  async function handleHideReply(replyId: string, hide: boolean) {
    if (!rantId || !post) return
    const before = post
    setActionError('')
    setPost(withReplyHidden(post, replyId, hide))
    try {
      await (hide ? hideReply(rantId, replyId) : unhideReply(rantId, replyId))
      await loadPost()
    } catch {
      setPost(before)
      setActionError(hide ? '沒有隱藏成功，請再試一次' : '沒有取消成功，請再試一次')
    }
  }

  function handleReplyToReply(replyId: string, nickname: string) {
    setReplyTarget({ id: replyId, nickname })
    setComposeFocused(true)
    setTimeout(() => inputRef.current?.focus(), 50)
  }

  const canReply = replyText.trim() || replyMedia.imageUrl || replyMedia.audioUrl

  if (!user) return null

  if (!post) {
    return (
      <div className="detail-page">
        <div className="detail-topbar">
          <button className="detail-back-btn" onClick={() => navigate(-1)}><ChevronLeft size={22} /></button>
          <span className="detail-topbar-title">貼文</span>
        </div>
        <p style={{ padding: '24px', color: '#8a9e9c' }}>
          {loaded ? '找不到這篇貼文，可能已被刪除' : '載入中…'}
        </p>
      </div>
    )
  }

  return (
    <div className="detail-page">
      {/* 頂部導覽 */}
      <div className="detail-topbar">
        <button className="detail-back-btn" onClick={() => navigate(-1)}><ChevronLeft size={22} /></button>
        <span className="detail-topbar-title">貼文</span>
        {/* 作者刪掉的貼文沒有東西可以複製連結、檢舉或再刪一次 */}
        {!post.isDeleted && (
          <PostMenu
            postId={post.id}
            isOwner={post.isMine}
            onDelete={async () => { await deleteRant(post.id); navigate(-1) }}
            onReport={() => setReportOpen(true)}
          />
        )}
      </div>

      <div className="detail-scroll">
        <div className="detail-card">
        {/* ── 主貼文 ──
            跟樹洞列表的卡片同一套：頭像＋名字時間、愛心和回應兩顆膠囊。
            檢舉在右上角的 ⋮ 裡（以前是動作列上一顆紅字按鈕）。 */}
        {post.isDeleted ? (
          /* 作者刪掉了這篇，但底下別人的回應留著（照 Threads 的做法）。
             原本貼文的位置只說一件事：這裡原本有一篇，現在沒有了。
             不留作者的名字、不留內容——後端送來的本來就是空的。 */
          <div className="detail-thread-post detail-deleted">
            <p className="detail-deleted-title">這則貼文已被作者刪除</p>
            <p className="detail-deleted-sub">底下的回應還留著</p>
          </div>
        ) : (
        <>
        <div className="detail-thread-post">
          <header className="rc-head">
            <span className={`rc-avatar rc-static tone-${avatarTone(post.nickname)}`} aria-hidden="true">
              {avatarLetter(post.nickname)}
            </span>
            <div className="rc-who rc-static">
              <span className="rc-name">{post.nickname}</span>
              <span className="rc-time" title={formatTime(post.createdAt)}>{timeAgo(post.createdAt)}</span>
            </div>
            <span className="tag rc-mode" data-mode={post.mode}>{MODE_LABELS[post.mode] ?? post.mode}</span>
          </header>

          <p className="detail-post-content">{post.content}</p>
          <PostImageGallery images={postImages(post)} />
          {post.audioUrl && <audio controls src={post.audioUrl} className="post-media-audio" />}

          {post.hashtags?.length > 0 && (
            <div className="hashtag-row">
              {post.hashtags.map((tag) => <span key={tag} className="hashtag-tag">#{tag}</span>)}
            </div>
          )}

          <div className="rc-actions">
            <HeartButton
              className="rc-pill"
              liked={!!post.isLiked}
              count={post.likeCount}
              onToggle={handleUnderstand}
              label="我懂"
            />
            <button
              type="button"
              className="rc-pill"
              onClick={() => { setComposeFocused(true); setTimeout(() => inputRef.current?.focus(), 50) }}
            >
              <MessageCircle size={16} />
              <span>回應{post.replyCount > 0 ? ` ${post.replyCount}` : ''}</span>
            </button>
          </div>
        </div>

        {/* ── 回覆輸入區（提示 → 快捷回覆 → 輸入＋送出）── */}
        <div className="detail-thread-compose">
          {replyTarget && (
            <div className="reply-target-hint" style={{ padding: '2px 14px' }}>↩ 回覆 @{replyTarget.nickname}
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

          {/* 輸入框 + 送出鈕 */}
          <div className="reply-input-row">
            <input
              ref={inputRef}
              className="reply-input"
              value={replyText}
              onChange={(e) => setReplyText(e.target.value)}
              onFocus={() => setComposeFocused(true)}
              placeholder={replyTarget ? `回覆 @${replyTarget.nickname}…` : placeholder}
            />
            <button className="reply-send-btn" onClick={handleReply} disabled={!canReply || submitting} aria-label="送出">
              <ArrowUp size={18} strokeWidth={2.2} />
            </button>
          </div>

          {composeFocused && (
            <div className="reply-extra-row">
              <MediaInput value={replyMedia} onChange={setReplyMedia} />
              <div className="reply-form-actions">
                <button className="ghost" onClick={() => { setComposeFocused(false); setReplyTarget(null); setReplyText(''); setReplyMedia(EMPTY_MEDIA) }}>取消</button>
              </div>
            </div>
          )}
        </div>
        </>
        )}

        {/* ── 回覆列表 ── */}
        {actionError && <p className="rc-thread-note detail-action-error" role="alert">{actionError}</p>}
        {post.replies.length > 1 && (
          <div className="rc-sort-row detail-sort-row"><ReplySortMenu /></div>
        )}
        {post.replies.length > 0 && (
          <div className="detail-thread-reply-list">
            {sortReplies(post.replies, replySort).map((reply) => (
              <div key={reply.id} className="detail-thread-reply-item">
                <ReplyItem
                  reply={reply}
                  // 貼文刪掉之後不能再接話；既有的回應還能按愛心、還能收回自己的
                  onReply={post.isDeleted ? undefined : handleReplyToReply}
                  onLike={(replyId, like) => (rantId
                    ? (like ? likeReply(rantId, replyId) : unlikeReply(rantId, replyId)).then(loadPost)
                    : undefined)}
                  onDelete={(replyId) => (rantId ? deleteReply(rantId, replyId).then(loadPost) : undefined)}
                  onHide={post.isMine ? handleHideReply : undefined}
                />
              </div>
            ))}
          </div>
        )}
        </div>
      </div>

      {reportOpen && post && (
        <ReportSheet
          targetType="RANT_POST"
          targetId={post.id}
          onClose={() => setReportOpen(false)}
        />
      )}
    </div>
  )
}
