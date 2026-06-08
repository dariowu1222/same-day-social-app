import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Heart, MessageCircle, ChevronLeft, Flag } from 'lucide-react'
import type { DemoUser } from '../App'
import { deleteRant, getRants, replyRant, reportRant, understandRant, type RantPost } from '../api/client'
import MediaInput, { EMPTY_MEDIA, type MediaState } from '../components/MediaInput'
import ReplyItem from '../components/ReplyItem'
import PostMenu from '../components/PostMenu'

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

type Props = { user: DemoUser }

export default function RantDetailPage({ user }: Props) {
  const { rantId } = useParams<{ rantId: string }>()
  const navigate = useNavigate()
  const [post, setPost] = useState<RantPost | null>(null)
  const [replyText, setReplyText] = useState('')
  const [replyMedia, setReplyMedia] = useState<MediaState>(EMPTY_MEDIA)
  const [replyTarget, setReplyTarget] = useState<{ id: string; nickname: string } | null>(null)
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    loadPost()
  }, [rantId])

  async function loadPost() {
    try {
      const res = await getRants()
      const found = res.data.find((p) => p.id === rantId) ?? null
      setPost(found)
    } catch {
      // ignore
    }
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
    if (!rantId || submitting) return
    setSubmitting(true)
    try {
      await replyRant(rantId, {
        userId: user.userId,
        nickname: user.nickname,
        content: replyText,
        ...replyMedia,
        parentReplyId: replyTarget?.id ?? null,
      })
      setReplyText('')
      setReplyMedia(EMPTY_MEDIA)
      setReplyTarget(null)
      await loadPost()
    } finally {
      setSubmitting(false)
    }
  }

  function handleReplyToReply(replyId: string, nickname: string) {
    setReplyTarget({ id: replyId, nickname })
    document.querySelector<HTMLInputElement>('.detail-reply-input-wrap input')?.focus()
  }

  const canReply = replyText.trim() || replyMedia.imageDataUrl || replyMedia.audioDataUrl

  if (!post) {
    return (
      <div className="detail-page">
        <div className="detail-topbar">
          <button className="detail-back-btn" onClick={() => navigate(-1)}>
            <ChevronLeft size={22} />
          </button>
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
        <button className="detail-back-btn" onClick={() => navigate(-1)}>
          <ChevronLeft size={22} />
        </button>
        <span className="detail-topbar-title">貼文</span>
        {post && (
          <PostMenu
            postId={post.id}
            isOwner={post.userId === user.userId}
            onDelete={async () => { await deleteRant(post.id, user.userId); navigate(-1) }}
          />
        )}
      </div>

      <div className="detail-scroll">
        {/* 主貼文 */}
        <div className="detail-post">
          <div className="post-author-row">
            <div className="avatar-circle">{avatarLetter(post.nickname)}</div>
            <div className="post-author-info">
              <span className="post-author-name">{post.nickname}</span>
              <span className="post-time">{formatTime(post.createdAt)}</span>
            </div>
            <span className="tag">{MODE_LABELS[post.mode] ?? post.mode}</span>
          </div>

          <p className="detail-post-content">{post.content}</p>

          {post.imageDataUrl && <img src={post.imageDataUrl} className="post-media-img" alt="" />}
          {post.audioDataUrl && <audio controls src={post.audioDataUrl} className="post-media-audio" />}

          {post.hashtags?.length > 0 && (
            <div className="hashtag-row">
              {post.hashtags.map((tag) => <span key={tag} className="hashtag-tag">#{tag}</span>)}
            </div>
          )}
          {post.emotionTags.length > 0 && (
            <div className="tag-row">
              {post.emotionTags.map((tag) => <span key={tag} className="tag">{tag}</span>)}
            </div>
          )}

          <div className="detail-action-row">
            <button className="post-action-btn" onClick={handleUnderstand}>
              <Heart size={16} /> {post.likeCount}
            </button>
            <button className="post-action-btn">
              <MessageCircle size={16} /> {post.replies.length}
            </button>
            <button className="post-action-btn report-btn" onClick={handleReport}>
              <Flag size={14} /> 檢舉
            </button>
          </div>
        </div>

        {/* 回覆輸入框 */}
        <div className="detail-reply-form">
          <div className="avatar-circle avatar-sm">{avatarLetter(user.nickname)}</div>
          <div className="detail-reply-input-wrap">
            {replyTarget && (
              <div className="reply-target-hint">↩ 回覆 @{replyTarget.nickname}
                <button className="reply-target-clear" onClick={() => setReplyTarget(null)}>×</button>
              </div>
            )}
            <input
              value={replyText}
              onChange={(e) => setReplyText(e.target.value)}
              placeholder={replyTarget ? `回覆 @${replyTarget.nickname}…` : '留下回應…'}
            />
            <MediaInput value={replyMedia} onChange={setReplyMedia} />
          </div>
          <button
            className="secondary"
            onClick={handleReply}
            disabled={!canReply || submitting}
          >
            送出
          </button>
        </div>

        {/* 第一層回覆（子回覆各自展開） */}
        {post.replies.length > 0 && (
          <div className="detail-reply-list">
            {post.replies.map((reply) => (
              <ReplyItem
                key={reply.id}
                reply={reply}
                onReply={handleReplyToReply}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
