import type { ChatMessage, ChatRoom as ChatRoomType } from '../api/client'

type Props = {
  room: ChatRoomType | null
  messages: ChatMessage[]
  draft: string
  onDraftChange: (value: string) => void
  onSend: () => void
}

export default function ChatRoom({ room, messages, draft, onDraftChange, onSend }: Props) {
  if (!room) {
    return <section className="panel empty-state">還沒有聊天室。雙方都按「想聊聊」後，這裡會出現對話。</section>
  }

  return (
    <section className="panel chat-panel">
      <h2>聊天室</h2>
      <div className="message-list">
        {messages.map((message) => (
          <p key={message.id} className="message">
            {message.content}
          </p>
        ))}
      </div>
      <div className="inline-form">
        <input value={draft} onChange={(event) => onDraftChange(event.target.value)} placeholder="慢慢說，不急。" />
        <button onClick={onSend} disabled={!draft.trim()}>
          送出
        </button>
      </div>
    </section>
  )
}
