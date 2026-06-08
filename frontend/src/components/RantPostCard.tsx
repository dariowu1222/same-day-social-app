import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Heart, MessageCircle } from 'lucide-react'
import { flattenReplies, type RantPost } from '../api/client'
import MediaInput, { type MediaState } from './MediaInput'

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
  onReply: () => void
  onReport: () => void
}

export default function RantPostCard({
  post, replyText, replyMedia,
  onReplyTextChange, onReplyMediaChange,
  onUnderstand, onReply, onReport,
}: Props) {
  const navigate = useNavigate()
  const [showReplies, setShowReplies] = useState(false)
  const [showReplyForm, setShowReplyForm] = useState(false)
  const canReply = replyText.trim() || replyMedia.imageDataUrl || replyMedia.audioDataUrl

  function handleReply() {
    onReply()
    setShowReplyForm(false)
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
        </div>

        {/* 內容 */}
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
        <button className="post-action-btn" onClick={(e) => { e.stopPropagation(); setShowReplies(true); setShowReplyForm(true) }}>
          <MessageCircle size={16} /> {post.replies.length}
        </button>
        <button className="post-action-btn report-btn" onClick={onReport}>檢舉</button>
      </div>

      {/* 回覆列表（遞迴展平，預設收合） */}
      {post.replies.length > 0 && (
        <button className="reply-toggle-btn" onClick={() => setShowReplies((v) => !v)}>
          {showReplies ? '收起回應' : `查看 ${flattenReplies(post.replies).length} 則回應`}
        </button>
      )}

      {showReplies && (
        <div className="flat-reply-list">
          {flattenReplies(post.replies).map((reply) => (
            <div key={reply.id} className="flat-reply-item">
              <div className="avatar-circle avatar-sm">{avatarLetter(reply.nickname)}</div>
              <div className="flat-reply-body">
                <span className="post-author-name">{reply.nickname}</span>
                <p className="flat-reply-content">{reply.content}</p>
                {reply.imageDataUrl && <img src={reply.imageDataUrl} className="reply-media-img" alt="" />}
                {reply.audioDataUrl && <audio controls src={reply.audioDataUrl} className="post-media-audio" />}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* 回覆表單 */}
      {showReplyForm ? (
        <div className="reply-form">
          <input
            value={replyText}
            onChange={(e) => onReplyTextChange(e.target.value)}
            placeholder="留一句溫和回應"
            autoFocus
          />
          <MediaInput value={replyMedia} onChange={onReplyMediaChange} />
          <div className="reply-form-actions">
            <button className="ghost" onClick={() => setShowReplyForm(false)}>取消</button>
            <button className="secondary" onClick={handleReply} disabled={!canReply}>送出</button>
          </div>
        </div>
      ) : (
        <button className="reply-open-btn" onClick={() => { setShowReplyForm(true); setShowReplies(true) }}>
          留下回應…
        </button>
      )}
    </article>
  )
}
