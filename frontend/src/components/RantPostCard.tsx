import type { RantPost } from '../api/client'

type Props = {
  post: RantPost
  replyText: string
  onReplyTextChange: (value: string) => void
  onUnderstand: () => void
  onReply: () => void
  onReport: () => void
}

export default function RantPostCard({ post, replyText, onReplyTextChange, onUnderstand, onReply, onReport }: Props) {
  return (
    <article className="card">
      <div className="card-title-row">
        <h3>{post.nickname}</h3>
        <span className="tag">{post.mode}</span>
      </div>
      <p>{post.content}</p>
      <div className="tag-row">
        {post.emotionTags.map((tag) => (
          <span key={tag} className="tag">
            {tag}
          </span>
        ))}
      </div>
      <div className="button-row">
        <button className="secondary" onClick={onUnderstand}>
          我懂 {post.likeCount}
        </button>
        <button className="ghost" onClick={onReport}>
          檢舉
        </button>
      </div>
      {post.replies.length > 0 && (
        <div className="reply-list">
          {post.replies.map((reply) => (
            <p key={reply.id}>
              <strong>{reply.nickname}</strong>：{reply.content}
            </p>
          ))}
        </div>
      )}
      <div className="inline-form">
        <input value={replyText} onChange={(event) => onReplyTextChange(event.target.value)} placeholder="留一句溫和回應" />
        <button className="secondary" onClick={onReply} disabled={!replyText.trim()}>
          留言
        </button>
      </div>
    </article>
  )
}
