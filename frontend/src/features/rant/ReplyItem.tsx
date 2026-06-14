import { useState } from 'react'
import { Heart } from 'lucide-react'
import { flattenReplies, type RantReply } from './types'

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

  // 建立 id→reply 查找 map，用於顯示引用框
  const replyMap = new Map<string, RantReply>()
  replyMap.set(reply.id, reply)
  subReplies.forEach((s) => replyMap.set(s.id, s))

  // 若子回覆的 parentReplyId 不是第一層 reply，代表是回覆另一則子回覆，需要引用框
  function getQuoteParent(sub: RantReply): RantReply | null {
    if (!sub.parentReplyId || sub.parentReplyId === reply.id) return null
    return replyMap.get(sub.parentReplyId) ?? null
  }

  return (
    <div className="thread-row" style={{ paddingTop: 10 }}>
      {/* 左：頭像 + 串文線 */}
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

        <div className="thread-action-bar">
          <button className="reply-like-btn" onClick={() => onLike(reply.id)}>
            <Heart size={14} /> {reply.likeCount > 0 ? reply.likeCount : ''}
          </button>
          <button className="reply-sub-btn" onClick={() => onReply(reply.id, reply.nickname)}>回應</button>
        </div>

        {/* 子回覆：收合態 */}
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

        {/* 子回覆：展開態（含引用框）*/}
        {showSubs && (
          <div className="thread-sub-list">
            {subReplies.map((sub) => {
              const quoteParent = getQuoteParent(sub)
              return (
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

                    {/* 引用框：只在回覆子回覆時顯示 */}
                    {quoteParent && (
                      <div className="quote-box">
                        <span className="quote-author">{quoteParent.nickname}：</span>
                        <span className="quote-text">{quoteParent.content}</span>
                      </div>
                    )}

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
              )
            })}
            <button className="thread-collapse-btn" onClick={() => setShowSubs(false)}>收起</button>
          </div>
        )}
      </div>
    </div>
  )
}
