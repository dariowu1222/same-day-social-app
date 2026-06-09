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
    <div className="thread-row" style={{ paddingTop: 10 }}>
      {/* 左：頭像 + 串文線（有子回覆時才顯示）*/}
      <div className="thread-left-sm">
        <div className="avatar-circle avatar-sm">{avatarLetter(reply.nickname)}</div>
        {showSubs && subReplies.length > 0 && <div className="thread-line" />}
      </div>

      {/* 右：內容 */}
      <div className="thread-reply-right">
        <div className="thread-reply-header">
          <span className="post-author-name">{reply.nickname}</span>
        </div>

        <p className="flat-reply-content">{reply.content}</p>
        {reply.imageDataUrl && <img src={reply.imageDataUrl} className="reply-media-img" alt="" />}
        {reply.audioDataUrl && <audio controls src={reply.audioDataUrl} className="post-media-audio" />}

        {/* 動作列 */}
        <div className="thread-action-bar">
          <button className="reply-like-btn" onClick={() => onLike(reply.id)}>
            <Heart size={14} /> {reply.likeCount > 0 ? reply.likeCount : ''}
          </button>
          <button className="reply-sub-btn" onClick={() => onReply(reply.id, reply.nickname)}>回應</button>
        </div>

        {/* 子回覆：收合態 — 堆疊小頭像 */}
        {!showSubs && subReplies.length > 0 && (
          <div className="thread-sub-indicator" onClick={() => setShowSubs(true)}>
            <div className="thread-sub-avatars">
              {subReplies.slice(0, 3).map((s) => (
                <div key={s.id} className="thread-sub-avatar-xs">{avatarLetter(s.nickname)}</div>
              ))}
            </div>
            <span className="thread-sub-count">查看 {subReplies.length} 則回應</span>
          </div>
        )}

        {/* 子回覆：展開態 */}
        {showSubs && (
          <div className="thread-sub-list">
            {subReplies.map((sub) => (
              <div key={sub.id} className="thread-row" style={{ paddingTop: 8 }}>
                <div className="thread-left-sm">
                  <div className="avatar-circle avatar-sm" style={{ background: '#5b8f8a' }}>
                    {avatarLetter(sub.nickname)}
                  </div>
                </div>
                <div style={{ flex: 1, minWidth: 0, paddingBottom: 8 }}>
                  <div className="thread-reply-header">
                    <span className="post-author-name">{sub.nickname}</span>
                  </div>
                  <p className="flat-reply-content">{sub.content}</p>
                  {sub.imageDataUrl && <img src={sub.imageDataUrl} className="reply-media-img" alt="" />}
                  {sub.audioDataUrl && <audio controls src={sub.audioDataUrl} className="post-media-audio" />}
                  <div className="thread-action-bar">
                    <button className="reply-like-btn" onClick={() => onLike(sub.id)}>
                      <Heart size={13} /> {sub.likeCount > 0 ? sub.likeCount : ''}
                    </button>
                    <button className="reply-sub-btn" onClick={() => onReply(sub.id, sub.nickname)}>回應</button>
                  </div>
                </div>
              </div>
            ))}
            <button className="thread-collapse-btn" onClick={() => setShowSubs(false)}>收起</button>
          </div>
        )}
      </div>
    </div>
  )
}
