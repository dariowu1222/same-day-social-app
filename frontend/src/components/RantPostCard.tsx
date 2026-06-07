import { useState } from 'react'
import type { RantPost } from '../api/client'

type Reply = RantPost['replies'][number]

type Props = {
  post: RantPost
  currentUserId: string
  onUnderstand: () => void
  onReply: (content: string, parentReplyId?: string) => void
  onReport: () => void
}

const modeLabels: Record<string, string> = {
  JUST_SAYING: '只是說說',
  COMFORT_ME: '想被安慰',
  GIVE_ADVICE: '想聽建議',
  RANT_TOGETHER: '想一起氣',
  DISTRACT_ME: '想轉移注意力',
  FIND_SIMILAR: '想找同類',
}

const modeEmojis: Record<string, string> = {
  JUST_SAYING: '🌙',
  COMFORT_ME: '🫂',
  GIVE_ADVICE: '💡',
  RANT_TOGETHER: '😤',
  DISTRACT_ME: '✨',
  FIND_SIMILAR: '🔍',
}

function timeAgo(dateStr: string) {
  const diff = Date.now() - new Date(dateStr).getTime()
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return '剛剛'
  if (mins < 60) return `${mins} 分鐘前`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs} 小時前`
  return `${Math.floor(hrs / 24)} 天前`
}

function Avatar({ nickname }: { nickname: string }) {
  return (
    <div className="rant-reply-avatar">
      {nickname === '匿名' ? '👤' : nickname.charAt(0)}
    </div>
  )
}

/** @mention 高亮 */
function RenderContent({ text }: { text: string }) {
  const parts = text.split(/(@\S+)/g)
  return (
    <>
      {parts.map((part, i) =>
        part.startsWith('@')
          ? <span key={i} className="rant-mention">{part}</span>
          : <span key={i}>{part}</span>
      )}
    </>
  )
}

/** 遞迴渲染單則回覆 + 其子回覆 */
function ReplyItem({
  reply,
  allReplies,
  depth,
  replyingToId,
  replyText,
  onReplyTextChange,
  onStartReply,
  onSendReply,
}: {
  reply: Reply
  allReplies: Reply[]
  depth: number
  replyingToId: string | null
  replyText: string
  onReplyTextChange: (v: string) => void
  onStartReply: (id: string, nickname: string) => void
  onSendReply: () => void
}) {
  const children = allReplies.filter(r => r.parentReplyId === reply.id)
  const isReplying = replyingToId === reply.id
  // 最多縮排 3 層，避免太窄
  const indent = Math.min(depth, 3) * 20

  return (
    <div className="rant-thread-item" style={{ marginLeft: indent }}>
      {/* 左側豎線（非第 0 層才畫） */}
      {depth > 0 && <div className="rant-thread-line" />}

      <div className="rant-thread-row">
        <Avatar nickname={reply.nickname} />
        <div className="rant-thread-body">
          <span className="rant-reply-name">{reply.nickname}</span>
          <p className="rant-reply-content">
            <RenderContent text={reply.content} />
          </p>
          <button
            className="rant-reply-action"
            onClick={() => onStartReply(reply.id, reply.nickname)}
          >
            回應
          </button>
        </div>
      </div>

      {/* 此則 reply 的輸入框 */}
      {isReplying && (
        <div className="rant-reply-input" style={{ marginLeft: 36, marginTop: 6 }}>
          <input
            autoFocus
            value={replyText}
            onChange={e => onReplyTextChange(e.target.value)}
            placeholder={`回覆 ${reply.nickname}…`}
          />
          <button
            className="rant-reply-send"
            onClick={onSendReply}
            disabled={!replyText.trim()}
          >送出</button>
        </div>
      )}

      {/* 子回覆（遞迴） */}
      {children.map(child => (
        <ReplyItem
          key={child.id}
          reply={child}
          allReplies={allReplies}
          depth={depth + 1}
          replyingToId={replyingToId}
          replyText={replyText}
          onReplyTextChange={onReplyTextChange}
          onStartReply={onStartReply}
          onSendReply={onSendReply}
        />
      ))}
    </div>
  )
}

export default function RantPostCard({ post, onUnderstand, onReply, onReport }: Props) {
  const [showReplies, setShowReplies] = useState(false)
  const [showMainReply, setShowMainReply] = useState(false)
  const [replyingToId, setReplyingToId] = useState<string | null>(null)
  const [replyText, setReplyText] = useState('')

  // 頂層回覆（沒有 parentReplyId）
  const rootReplies = post.replies.filter(r => !r.parentReplyId)

  function handleStartReply(id: string, nickname: string) {
    setReplyingToId(id)
    setReplyText(`@${nickname} `)
    setShowMainReply(false)
    if (!showReplies) setShowReplies(true)
  }

  function handleOpenMainReply() {
    setReplyingToId(null)
    setReplyText('')
    setShowMainReply(v => !v)
  }

  function handleSend(parentReplyId?: string) {
    if (!replyText.trim()) return
    onReply(replyText, parentReplyId)
    setReplyText('')
    setReplyingToId(null)
    setShowMainReply(false)
  }

  return (
    <article className="rant-card">
      {/* 頭部 */}
      <div className="rant-card-head">
        <div className="rant-card-avatar">
          {post.nickname === '匿名' ? '👤' : post.nickname.charAt(0)}
        </div>
        <div className="rant-card-meta">
          <span className="rant-card-name">{post.nickname}</span>
          <span className="rant-card-time">{timeAgo(post.createdAt)}</span>
        </div>
        {post.mode && (
          <span className="rant-card-mode">
            {modeEmojis[post.mode] ?? ''} {modeLabels[post.mode] ?? post.mode}
          </span>
        )}
      </div>

      {/* 內容 */}
      <p className="rant-card-content">{post.content}</p>

      {/* 情緒標籤 */}
      {post.emotionTags.length > 0 && (
        <div className="rant-card-tags">
          {post.emotionTags.map(tag => (
            <span key={tag} className="rant-card-tag">{tag}</span>
          ))}
        </div>
      )}

      {/* 操作列 */}
      <div className="rant-card-actions">
        <button
          className={`rant-action-btn${post.likedByMe ? ' rant-action-liked' : ''}`}
          onClick={onUnderstand}
        >
          <svg width="17" height="17" viewBox="0 0 17 17" fill={post.likedByMe ? 'currentColor' : 'none'}>
            <path d="M8.5 14s-6-3.8-6-8a4 4 0 0 1 6-3.46A4 4 0 0 1 14.5 6c0 4.2-6 8-6 8z"
              stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
          {post.likeCount > 0 && <span>{post.likeCount}</span>}
        </button>

        <button className="rant-action-btn" onClick={handleOpenMainReply}>
          <svg width="17" height="17" viewBox="0 0 17 17" fill="none">
            <path d="M2 3h13v9H9l-4 3v-3H2z" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
          {post.replyCount > 0 && <span>{post.replyCount}</span>}
          <span className="rant-action-label">回應</span>
        </button>

        <button className="rant-action-report" onClick={onReport}>檢舉</button>
      </div>

      {/* 針對原文的輸入框 */}
      {showMainReply && (
        <div className="rant-reply-input">
          <input
            autoFocus
            value={replyText}
            onChange={e => setReplyText(e.target.value)}
            placeholder="留一句溫和的話…"
          />
          <button
            className="rant-reply-send"
            onClick={() => handleSend(undefined)}
            disabled={!replyText.trim()}
          >送出</button>
        </div>
      )}

      {/* 回覆列表 */}
      {post.replies.length > 0 && (
        <div className="rant-replies">
          <button className="rant-replies-toggle" onClick={() => setShowReplies(v => !v)}>
            {showReplies ? '收起' : `查看 ${post.replies.length} 則回應`}
          </button>

          {showReplies && (
            <div className="rant-thread-list">
              {rootReplies.map(reply => (
                <ReplyItem
                  key={reply.id}
                  reply={reply}
                  allReplies={post.replies}
                  depth={0}
                  replyingToId={replyingToId}
                  replyText={replyText}
                  onReplyTextChange={setReplyText}
                  onStartReply={handleStartReply}
                  onSendReply={() => handleSend(replyingToId ?? undefined)}
                />
              ))}
            </div>
          )}
        </div>
      )}
    </article>
  )
}
