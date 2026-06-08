import type { RantPost } from '../api/client'
import MediaInput, { type MediaState } from './MediaInput'

const MODE_LABELS: Record<string, string> = {
  JUST_SAYING: '只是想說',
  COMFORT_ME: '想被安慰',
  GIVE_ADVICE: '想聽建議',
  RANT_TOGETHER: '想一起抱怨',
  DISTRACT_ME: '想轉移注意力',
  FIND_SIMILAR: '想找同類',
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

export default function RantPostCard({ post, replyText, replyMedia, onReplyTextChange, onReplyMediaChange, onUnderstand, onReply, onReport }: Props) {
  const canReply = replyText.trim() || replyMedia.imageDataUrl || replyMedia.audioDataUrl

  return (
    <article className="card">
      <div className="card-title-row">
        <h3>{post.nickname}</h3>
        <span className="tag">{MODE_LABELS[post.mode] ?? post.mode}</span>
      </div>
      <p>{post.content}</p>

      {post.imageDataUrl && (
        <img src={post.imageDataUrl} className="post-media-img" alt="貼文圖片" />
      )}
      {post.audioDataUrl && (
        <audio controls src={post.audioDataUrl} className="post-media-audio" />
      )}

      {post.hashtags?.length > 0 && (
        <div className="hashtag-row">
          {post.hashtags.map((tag) => (
            <span key={tag} className="hashtag-tag">#{tag}</span>
          ))}
        </div>
      )}
      <div className="tag-row">
        {post.emotionTags.map((tag) => (
          <span key={tag} className="tag">{tag}</span>
        ))}
      </div>
      <div className="button-row">
        <button className="secondary" onClick={onUnderstand}>
          我懂 {post.likeCount}
        </button>
        <button className="ghost report-btn" onClick={onReport}>
          檢舉
        </button>
      </div>

      {post.replies.length > 0 && (
        <div className="reply-list">
          {post.replies.map((reply) => (
            <div key={reply.id} className="reply-item">
              <p><strong>{reply.nickname}</strong>：{reply.content}</p>
              {reply.imageDataUrl && (
                <img src={reply.imageDataUrl} className="reply-media-img" alt="回覆圖片" />
              )}
              {reply.audioDataUrl && (
                <audio controls src={reply.audioDataUrl} className="post-media-audio" />
              )}
            </div>
          ))}
        </div>
      )}

      <div className="reply-form">
        <input
          value={replyText}
          onChange={(event) => onReplyTextChange(event.target.value)}
          placeholder="留一句溫和回應"
        />
        <MediaInput value={replyMedia} onChange={onReplyMediaChange} />
        <button className="secondary" onClick={onReply} disabled={!canReply}>
          留言
        </button>
      </div>
    </article>
  )
}
