import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Heart, MessageCircle, Flag } from 'lucide-react'
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
  onHashtagClick?: (tag: string) => void
  currentUserId: string
}

export default function RantPostCard({
  post, replyText, replyMedia,
  onReplyTextChange, onReplyMediaChange,
  onUnderstand, onReply, onReport, onDelete, onLikedReply, onHashtagClick, currentUserId,
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
    <article className="card" style={{ gap: 0, padding: '14px 14px 8px' }}>
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
              <PostMenu postId={post.id} isOwner={post.userId === currentUserId} onDelete={onDelete} />
            </div>
          </div>

          {/* 內文（點擊進詳細頁）*/}
          <div onClick={() => navigate(`/rant/${post.id}`)} style={{ cursor: 'pointer' }}>
            <p className="post-content" style={{ marginBottom: 8 }}>{post.content}</p>
            {post.imageDataUrl && <img src={post.imageDataUrl} className="post-media-img" alt="貼文圖片" />}
            {post.audioDataUrl && <audio controls src={post.audioDataUrl} className="post-media-audio" />}
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
            <button className="post-action-btn" onClick={(e) => { e.stopPropagation(); openReplyToPost() }}>
              <MessageCircle size={16} /> {post.replyCount > 0 ? post.replyCount : ''}
            </button>
            <button className="post-action-btn report-btn" onClick={onReport}>
              <Flag size={14} /> 檢舉
            </button>
          </div>
        </div>
      </div>

      {/* 回覆輸入區 */}
      {showReplyForm ? (
        <div className="reply-form" style={{ marginTop: 6 }}>
          {replyTarget && (
            <div className="reply-target-hint">↩ 回覆 @{replyTarget.nickname}
              <button className="reply-target-clear" onClick={() => setReplyTarget(null)}>×</button>
            </div>
          )}
          <input
            value={replyText}
            onChange={(e) => onReplyTextChange(e.target.value)}
            placeholder={replyTarget ? `回覆 @${replyTarget.nickname}…` : '留一句溫和回應'}
            autoFocus
          />
          <MediaInput value={replyMedia} onChange={onReplyMediaChange} />
          <div className="reply-form-actions">
            <button className="ghost" onClick={() => { setShowReplyForm(false); setReplyTarget(null) }}>取消</button>
            <button className="secondary" onClick={handleReply} disabled={!canReply}>送出</button>
          </div>
        </div>
      ) : (
        <button className="thread-compose-btn" onClick={openReplyToPost}>
          {replyTarget ? `回覆 @${replyTarget.nickname}…` : '留下回應…'}
        </button>
      )}

      {/* 回覆收合切換 */}
      {post.replies.length > 0 && !showReplies && (
        <button className="reply-toggle-btn" style={{ paddingTop: 4 }} onClick={() => setShowReplies(true)}>
          查看 {post.replyCount} 則回應
        </button>
      )}

      {/* 回覆列表 */}
      {showReplies && (
        <div className="thread-reply-section">
          {post.replies.map((reply) => (
            <ReplyItem
              key={reply.id}
              reply={reply}
              onReply={openReplyToReply}
              onLike={(replyId) => likeReply(post.id, replyId).then(onLikedReply)}
            />
          ))}
          <button className="thread-collapse-btn" onClick={() => setShowReplies(false)}>
            收起回應
          </button>
        </div>
      )}
    </article>
  )
}
