import { useEffect, useMemo, useState } from 'react'
import type { DemoUser } from '../App'
import { getChatRooms, getMessages, sendMessage, type ChatMessage, type ChatRoom as ChatRoomType } from '../api/client'
import ChatRoom from '../components/ChatRoom'

type Props = {
  user: DemoUser
}

export default function ChatPage({ user }: Props) {
  const [rooms, setRooms] = useState<ChatRoomType[]>([])
  const [activeRoomId, setActiveRoomId] = useState<string | null>(null)
  const [roomMessages, setRoomMessages] = useState<Record<string, ChatMessage[]>>({})
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [draft, setDraft] = useState('')

  useEffect(() => {
    loadRooms()
  }, [])

  useEffect(() => {
    if (activeRoomId) {
      loadMessages(activeRoomId)
    }
  }, [activeRoomId])

  async function loadRooms() {
    const response = await getChatRooms(user.userId)
    setRooms(response.data)
    const messagePairs = await Promise.all(
      response.data.map(async (room) => {
        const messageResponse = await getMessages(room.id)
        return [room.id, messageResponse.data] as const
      }),
    )
    setRoomMessages(Object.fromEntries(messagePairs))
  }

  async function loadMessages(roomId: string) {
    const response = await getMessages(roomId)
    setMessages(response.data)
  }

  async function submitMessage() {
    if (!activeRoomId) return
    await sendMessage(activeRoomId, draft)
    setDraft('')
    await loadMessages(activeRoomId)
    await loadRooms()
  }

  const activeRoom = rooms.find((room) => room.id === activeRoomId) ?? null
  const roomSummaries = useMemo(
    () => rooms
      .map((room, index) => buildRoomSummary(room, roomMessages[room.id] ?? [], user.userId, index))
      .sort((a, b) => b.lastAt.getTime() - a.lastAt.getTime()),
    [rooms, roomMessages, user.userId],
  )
  const todayRooms = roomSummaries.filter((summary) => isToday(summary.room.createdAt))
  const previousRooms = roomSummaries.filter((summary) => !isToday(summary.room.createdAt))

  if (activeRoom) {
    return (
      <div className="page chat-detail-page">
        <div className="chat-list-header">
          <button className="chat-back-button" type="button" onClick={() => setActiveRoomId(null)}>‹</button>
          <h1>{buildRoomTitle(activeRoom, user.userId)}</h1>
        </div>
        <ChatRoom room={activeRoom} messages={messages} draft={draft} onDraftChange={setDraft} onSend={submitMessage} />
      </div>
    )
  }

  if (rooms.length > 0) {
    return (
      <div className="page chat-list-page">
        <header className="chat-list-header">
          <h1>聊天</h1>
        </header>
        <section className="chat-list-scroll" aria-label="聊天列表">
          <ChatSection title="今天聊起的" rooms={todayRooms} onOpen={setActiveRoomId} />
          <ChatSection title="先前的對話" rooms={previousRooms} onOpen={setActiveRoomId} />
        </section>
      </div>
    )
  }

  return (
    <div className="page chat-empty-page">
      <header className="page-header">
        <p className="eyebrow">聊天</p>
        <h1>慢慢說就好。</h1>
      </header>
      <ChatRoom room={null} messages={[]} draft={draft} onDraftChange={setDraft} onSend={submitMessage} />
    </div>
  )
}

type ChatSummary = {
  room: ChatRoomType
  title: string
  avatarText: string
  avatarClass: string
  preview: string
  lastAt: Date
  timeLabel: string
  unreadCount: number
}

function ChatSection({ title, rooms, onOpen }: { title: string; rooms: ChatSummary[]; onOpen: (roomId: string) => void }) {
  if (rooms.length === 0) return null

  return (
    <section className="chat-section">
      <h2>{title}</h2>
      <div className="chat-card-list">
        {rooms.map((summary) => (
          <button key={summary.room.id} className="chat-list-card" type="button" onClick={() => onOpen(summary.room.id)}>
            <span className={`chat-avatar ${summary.avatarClass}`}>{summary.avatarText}</span>
            <span className="chat-card-main">
              <span className="chat-card-name-row">
                <strong>{summary.title}</strong>
                <time>{summary.timeLabel}</time>
              </span>
              <span className={`chat-card-preview${summary.unreadCount > 0 ? ' unread' : ''}`}>{summary.preview}</span>
            </span>
            {summary.unreadCount > 0 && (
              <span className="chat-unread-badge">{summary.unreadCount > 99 ? '99+' : summary.unreadCount}</span>
            )}
          </button>
        ))}
      </div>
    </section>
  )
}

function buildRoomSummary(room: ChatRoomType, messages: ChatMessage[], currentUserId: string, index: number): ChatSummary {
  const lastMessage = messages[messages.length - 1]
  const title = buildRoomTitle(room, currentUserId)
  const lastAt = new Date(lastMessage?.createdAt ?? room.createdAt)

  return {
    room,
    title,
    avatarText: title.slice(0, 1),
    avatarClass: `tone-${index % 5}`,
    preview: formatMessagePreview(lastMessage),
    lastAt,
    timeLabel: formatRelativeTime(lastAt),
    unreadCount: 0,
  }
}

function buildRoomTitle(room: ChatRoomType, currentUserId: string) {
  const otherUserId = room.userIds.find((id) => id !== currentUserId)
  if (otherUserId) {
    return `同頻對話 ${otherUserId.slice(-4)}`
  }

  if (room.sourceType === 'TASK') return '任務對話'
  if (room.sourceType === 'RANT') return '樹洞對話'
  return '同頻對話'
}

function formatMessagePreview(message?: ChatMessage) {
  if (!message) return '還沒開始聊天'
  const content = message.content.trim()
  if (content === '[image]') return '[圖片]'
  if (content === '[sticker]') return '[貼圖]'
  return content || '還沒開始聊天'
}

function isToday(value: string) {
  const date = new Date(value)
  const now = new Date()
  return date.getFullYear() === now.getFullYear()
    && date.getMonth() === now.getMonth()
    && date.getDate() === now.getDate()
}

function formatRelativeTime(date: Date) {
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffMinutes = Math.max(0, Math.floor(diffMs / 60000))
  if (diffMinutes < 1) return '剛剛'
  if (diffMinutes < 60) return `${diffMinutes} 分前`

  const diffHours = Math.floor(diffMinutes / 60)
  if (diffHours < 24 && isToday(date.toISOString())) return `${diffHours} 小時前`

  const yesterday = new Date(now)
  yesterday.setDate(now.getDate() - 1)
  if (date.getFullYear() === yesterday.getFullYear()
    && date.getMonth() === yesterday.getMonth()
    && date.getDate() === yesterday.getDate()) {
    return '昨天'
  }

  const diffDays = Math.floor(diffHours / 24)
  if (diffDays < 7) {
    const weekday = ['日', '一', '二', '三', '四', '五', '六'][date.getDay()]
    return `週${weekday}`
  }

  return `${diffDays} 天前`
}
