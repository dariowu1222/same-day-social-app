import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Heart, MessageCircle } from 'lucide-react'
import { likeReply, type RantPost } from '../api/client'
import MediaInput, { type MediaState } from './MediaInput'
import ReplyItem from './ReplyItem'
import PostMenu from './PostMenu'

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

type Props = {
  post: RantPost
  replyText: string
  replyMedia: MediaState
  onReplyTextChange: (value: string) => void
  onReplyMediaChange: (media: MediaState) => void
  onUnderstand: () => void
  onReply: (parentReplyId?: string) => void
  onReport: () => void
  onDelete: () => void
  onLikedReply: () => void
  currentUserId: string
}

export default function RantPostCard({
  post, replyText, replyMedia,
  onReplyTextChange, onReplyMediaChange,
  onUnderstand, onReply, onReport, onDelete, onLikedReply, currentUserId,
}: Props) {
  const navigate = useNavigate()
  const [showReplies, setShowReplies] = useState(false)
  const [showReplyForm, setShowReplyForm] = useState(false)
  const [replyTarget, setReplyTarget] = useState<{ id: string; nickname: string } | null>(null)
  const canReply = replyText.trim() || replyMedia.imageDataUrl || replyMedia.audioDataUrl

  function handleReply() {
    onReply(replyTarget?.id)
    setShowReplyForm(false)
    setReplyTarget(null)
  }

  function openReplyToPost() {
    setReplyTarget(null)
    setShowReplyForm(true)
    setShowReplies(true)
  }

  function openReplyToReply(replyId: string, nickname: string) {
    setReplyTarget({ id: replyId, nickname })
    setShowReplyForm(true)
    setShowReplies(true)
  }

  return (
    <article className="card">
      {/* 貼文作者列 + 內容（點擊進詳細頁） */}
      <div className="card-clickable" onClick={() => navigate(`/rant/${post.id}`)}>
        <div className="post-author-row">
          <div className="avatar-circle">{avatarLetter(post.nickname)}</div>
          <div className="post-author-info">
            <span className="post-author-name">{post.nickname}</span>
            <span className="post-time">{new Date(post.createdAt).toLocaleDateString('zh-TW', { month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit' })}</span>
          </div>
          <span className="tag">{MODE_LABELS[post.mode] ?? post.mode}</span>
          <div onClick={(e) => e.stopPropagation()}>
            <PostMenu postId={post.id} isOwner={post.userId === currentUserId} onDelete={onDelete} />
          </div>
        </div>
        <p className="post-content">{post.content}</p>
        {post.imageDataUrl && <img src={post.imageDataUrl} className="post-media-img" alt="貼文圖片" />}
        {post.audioDataUrl && <audio controls src={post.audioDataUrl} className="post-media-audio" />}
      </div>

      {/* Tags */}
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

      {/* 動作列 */}
      <div className="post-action-row">
        <button className="post-action-btn" onClick={onUnderstand}>
          <Heart size={16} /> {post.likeCount}
        </button>
        <button className="post-action-btn" onClick={(e) => { e.stopPropagation(); openReplyToPost() }}>
          <MessageCircle size={16} /> {post.replyCount}
        </button>
        <button className="post-action-btn report-btn" onClick={onReport}>檢舉</button>
      </div>

      {/* 第一層回覆（預設收合，子回覆各自展開） */}
      {post.replies.length > 0 && (
        <button className="reply-toggle-btn" onClick={() => setShowReplies((v) => !v)}>
          {showReplies ? '收起回應' : `查看 ${post.replyCount} 則回應`}
        </button>
      )}

      {showReplies && (
        <div className="flat-reply-list">
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

      {/* 回覆表單 */}
      {showReplyForm ? (
        <div className="reply-form">
          {replyTarget && (
            <div className="reply-target-hint">↩ 回覆 @{replyTarget.nickname}</div>
          )}
          <input
            value={replyText}
            onChange={(e) => onReplyTextChange(e.target.value)}
            placeholder="留一句溫和回應"
            autoFocus
          />
          <MediaInput value={replyMedia} onChange={onReplyMediaChange} />
          <div className="reply-form-actions">
            <button className="ghost" onClick={() => { setShowReplyForm(false); setReplyTarget(null) }}>取消</button>
            <button className="secondary" onClick={handleReply} disabled={!canReply}>送出</button>
          </div>
        </div>
      ) : (
        <button className="reply-open-btn" onClick={openReplyToPost}>
          留下回應…
        </button>
      )}
    </article>
  )
}
