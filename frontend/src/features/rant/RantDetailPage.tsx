import { useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Heart, MessageCircle, ChevronLeft, Flag, HeartHandshake, ArrowUp, X } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { deleteRant, getRants, likeReply, replyRant, reportRant, understandRant } from './api'
import type { RantPost } from './types'
import MediaInput, { EMPTY_MEDIA, type MediaState } from '../../shared/ui/MediaInput'
import ReplyItem from './ReplyItem'
import PostMenu from './PostMenu'
import { COMPOSE_HINTS, QUICK_REPLIES, getHintSeenSet, markHintSeen, appendQuickReply } from './rantCompose'

const MODE_LABELS: Record<string, string> = {
  JUST_SAYING: '只是想說',
  COMFORT_ME: '想被安慰',
  GIVE_ADVICE: '想聽建議',
  RANT_TOGETHER: '想一起抱怨',
  DISTRACT_ME: '想轉移注意力',
  FIND_SIMILAR: '想找同類',
}

function avatarLetter(nickname: string) {
  return nickname.trim()[0]?.toUpperCase() ?? '?'
}

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
  const [replyText, setReplyText] = useState('')
  const [replyMedia, setReplyMedia] = useState<MediaState>(EMPTY_MEDIA)
  const [replyTarget, setReplyTarget] = useState<{ id: string; nickname: string } | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [composeFocused, setComposeFocused] = useState(false)
  const [showHint, setShowHint] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  const hintText = post ? COMPOSE_HINTS[post.mode] : undefined
  const quickReplies = post ? (QUICK_REPLIES[post.mode] ?? []) : []

  useEffect(() => { loadPost() }, [rantId])

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
    try {
      const res = await getRants()
      const found = res.data.find((p) => p.id === rantId) ?? null
      setPost(found)
    } catch { /* ignore */ }
  }

  async function handleUnderstand() {
    if (!rantId) return
    await understandRant(rantId)
    await loadPost()
  }

  async function handleReport() {
    if (!rantId) return
    await reportRant(rantId)
    await loadPost()
  }

  async function handleReply() {
    if (!rantId || submitting || !user) return
    setSubmitting(true)
    try {
      await replyRant(rantId, {
        nickname: user.nickname,
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

  function handleReplyToReply(replyId: string, nickname: string) {
    setReplyTarget({ id: replyId, nickname })
    setComposeFocused(true)
    setTimeout(() => inputRef.current?.focus(), 50)
  }

  const canReply = replyText.trim() || replyMedia.imageDataUrl || replyMedia.audioDataUrl

  if (!user) return null

  if (!post) {
    return (
      <div className="detail-page">
        <div className="detail-topbar">
          <button className="detail-back-btn" onClick={() => navigate(-1)}><ChevronLeft size={22} /></button>
          <span className="detail-topbar-title">貼文</span>
        </div>
        <p style={{ padding: '24px', color: '#8a9e9c' }}>載入中…</p>
      </div>
    )
  }

  return (
    <div className="detail-page">
      {/* 頂部導覽 */}
      <div className="detail-topbar">
        <button className="detail-back-btn" onClick={() => navigate(-1)}><ChevronLeft size={22} /></button>
        <span className="detail-topbar-title">貼文</span>
        <PostMenu
          postId={post.id}
          isOwner={post.userId === user.userId}
          onDelete={async () => { await deleteRant(post.id); navigate(-1) }}
        />
      </div>

      <div className="detail-scroll">
        <div className="detail-card">
        {/* ── 主貼文（Thread 風格）── */}
        <div className="detail-thread-post">
          <div className="thread-row">
            {/* 左：頭像 + 串文線（有回覆時）*/}
            <div className="thread-left">
              <div className="avatar-circle" style={{ flexShrink: 0 }}>{avatarLetter(post.nickname)}</div>
              {post.replies.length > 0 && <div className="thread-line" />}
            </div>

            {/* 右：貼文內容 */}
            <div className="thread-right">
              <div className="thread-post-header" style={{ cursor: 'default' }}>
                <div className="thread-post-header-info">
                  <span className="post-author-name">{post.nickname}</span>
                  <span className="post-time">{formatTime(post.createdAt)}</span>
                  <span className="tag">{MODE_LABELS[post.mode] ?? post.mode}</span>
                </div>
              </div>

              <p className="detail-post-content">{post.content}</p>
              {post.imageDataUrl && <img src={post.imageDataUrl} className="post-media-img" alt="" />}
              {post.audioDataUrl && <audio controls src={post.audioDataUrl} className="post-media-audio" />}

              {post.hashtags?.length > 0 && (
                <div className="hashtag-row">
                  {post.hashtags.map((tag) => <span key={tag} className="hashtag-tag">#{tag}</span>)}
                </div>
              )}


              {/* 動作列 */}
              <div className="thread-action-bar">
                <button className="post-action-btn" onClick={handleUnderstand}>
                  <Heart size={16} /> {post.likeCount > 0 ? post.likeCount : ''}
                </button>
                <button className="post-action-btn" onClick={() => { setComposeFocused(true); setTimeout(() => inputRef.current?.focus(), 50) }}>
                  <MessageCircle size={16} /> {post.replies.length > 0 ? post.replies.length : ''}
                </button>
                <button className="post-action-btn report-btn" onClick={handleReport}>
                  <Flag size={14} /> 檢舉
                </button>
              </div>
            </div>
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
              placeholder={replyTarget ? `回覆 @${replyTarget.nickname}…` : '留下回應...'}
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

        {/* ── 回覆列表 ── */}
        {post.replies.length > 0 && (
          <div className="detail-thread-reply-list">
            {post.replies.map((reply) => (
              <div key={reply.id} className="detail-thread-reply-item">
                <ReplyItem
                  reply={reply}
                  onReply={handleReplyToReply}
                  onLike={(replyId) => rantId && likeReply(rantId, replyId).then(loadPost)}
                />
              </div>
            ))}
          </div>
        )}
        </div>
      </div>
    </div>
  )
}
