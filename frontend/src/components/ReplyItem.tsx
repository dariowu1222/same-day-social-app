import { useState } from 'react'
import { Heart } from 'lucide-react'
import { flattenReplies, type RantReply } from '../api/client'

function avatarLetter(nickname: string) {
  return nickname.trim()[0]?.toUpperCase() ?? '?'
}

type Props = {
  reply: RantReply
  onReply: (replyId: string, nickname: string) => void
  onLike: (replyId: string) => void
}

export default function ReplyItem({ reply, onReply, onLike }: Props) {
  const [showSubs, setShowSubs] = useState(false)
  const subReplies = flattenReplies(reply.replies ?? [])

  return (
    <div className="flat-reply-item">
      <div className="avatar-circle avatar-sm">{avatarLetter(reply.nickname)}</div>
      <div className="flat-reply-body">
        <span className="post-author-name">{reply.nickname}</span>
        <p className="flat-reply-content">{reply.content}</p>
        {reply.imageDataUrl && <img src={reply.imageDataUrl} className="reply-media-img" alt="" />}
        {reply.audioDataUrl && <audio controls src={reply.audioDataUrl} className="post-media-audio" />}

        <div className="reply-item-actions">
          <button className="reply-like-btn" onClick={() => onLike(reply.id)}>
            <Heart size={13} /> {reply.likeCount > 0 ? reply.likeCount : ''}
          </button>
          {subReplies.length > 0 && (
            <button className="reply-sub-toggle" onClick={() => setShowSubs((v) => !v)}>
              {showSubs ? '收起' : `查看 ${subReplies.length} 則回應`}
            </button>
          )}
          <button className="reply-sub-btn" onClick={() => onReply(reply.id, reply.nickname)}>回應</button>
        </div>

        {showSubs && (
          <div className="sub-reply-list">
            {subReplies.map((sub) => (
              <div key={sub.id} className="flat-reply-item sub-reply-item">
                <div className="avatar-circle avatar-sm">{avatarLetter(sub.nickname)}</div>
                <div className="flat-reply-body">
                  <span className="post-author-name">{sub.nickname}</span>
                  <p className="flat-reply-content">{sub.content}</p>
                  {sub.imageDataUrl && <img src={sub.imageDataUrl} className="reply-media-img" alt="" />}
                  {sub.audioDataUrl && <audio controls src={sub.audioDataUrl} className="post-media-audio" />}
                  <div className="reply-item-actions">
                    <button className="reply-like-btn" onClick={() => onLike(sub.id)}>
                      <Heart size={13} /> {sub.likeCount > 0 ? sub.likeCount : ''}
                    </button>
                    <button className="reply-sub-btn" onClick={() => onReply(sub.id, sub.nickname)}>回應</button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
