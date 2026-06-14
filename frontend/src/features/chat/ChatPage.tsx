import { useEffect, useMemo, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { getChatRooms, getMessages, sendMessage } from './api'
import type { ChatMessage, ChatRoom as ChatRoomType } from './types'
import { getProfile } from '../profile/api'
import type { UserProfile } from '../profile/types'
import ChatRoom from './ChatRoom'
import Avatar from '../../shared/ui/Avatar'
import { testAvatarPhoto } from '../../shared/lib/userDisplay'

export default function ChatPage() {
  const { user } = useAuth()
  const [rooms, setRooms] = useState<ChatRoomType[]>([])
  const [activeRoomId, setActiveRoomId] = useState<string | null>(null)
  const [roomMessages, setRoomMessages] = useState<Record<string, ChatMessage[]>>({})
  const [profiles, setProfiles] = useState<Record<string, UserProfile>>({})
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
    if (!user) return
    const response = await getChatRooms(user.userId)
    setRooms(response.data)
    const messagePairs = await Promise.all(
      response.data.map(async (room) => {
        const messageResponse = await getMessages(room.id)
        return [room.id, messageResponse.data] as const
      }),
    )
    setRoomMessages(Object.fromEntries(messagePairs))

    // 載入每個對話中「對方」的個人資料，供頭像與自介使用
    const otherIds = Array.from(new Set(
      response.data
        .map((room) => room.userIds.find((id) => id !== user.userId))
        .filter((id): id is string => !!id),
    ))
    const profileEntries = await Promise.all(
      otherIds.map(async (id) => {
        try {
          const res = await getProfile(id)
          return [id, res.data] as const
        } catch {
          return null
        }
      }),
    )
    setProfiles(Object.fromEntries(profileEntries.filter((entry): entry is readonly [string, UserProfile] => !!entry)))
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
      .map((room) => buildRoomSummary(room, roomMessages[room.id] ?? [], profiles, user?.userId ?? ''))
      .sort((a, b) => b.lastAt.getTime() - a.lastAt.getTime()),
    [rooms, roomMessages, profiles, user?.userId],
  )
  const todayRooms = roomSummaries.filter((summary) => isToday(summary.room.createdAt))
  const previousRooms = roomSummaries.filter((summary) => !isToday(summary.room.createdAt))

  if (!user) return null

  if (activeRoom) {
    const otherUserId = activeRoom.userIds.find((id) => id !== user.userId) ?? activeRoom.id
    return (
      <ChatRoom
        room={activeRoom}
        otherProfile={profiles[otherUserId] ?? null}
        otherUserId={otherUserId}
        currentUserId={user.userId}
        messages={messages}
        draft={draft}
        onDraftChange={setDraft}
        onSend={submitMessage}
        onBack={() => setActiveRoomId(null)}
      />
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
      <section className="panel empty-state">還沒有聊天室。雙方都按「想聊聊」後，這裡會出現對話。</section>
    </div>
  )
}

type ChatSummary = {
  room: ChatRoomType
  title: string
  otherUserId: string
  avatarPhoto: string
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
            <Avatar photo={summary.avatarPhoto} seed={summary.otherUserId} className="chat-avatar" />
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

function buildRoomSummary(room: ChatRoomType, messages: ChatMessage[], profiles: Record<string, UserProfile>, currentUserId: string): ChatSummary {
  const lastMessage = messages[messages.length - 1]
  const otherUserId = room.userIds.find((id) => id !== currentUserId) ?? room.id
  const profile = profiles[otherUserId]
  const title = buildRoomTitle(room, profile, currentUserId)
  const lastAt = new Date(lastMessage?.createdAt ?? room.createdAt)

  return {
    room,
    title,
    otherUserId,
    // 正式環境用帳號 photoDataUrls[0]；測試先給寫死照片，失效時 Avatar 退回動物頭像
    avatarPhoto: profile?.photoDataUrls?.[0] ?? testAvatarPhoto(otherUserId),
    preview: formatMessagePreview(lastMessage),
    lastAt,
    timeLabel: formatRelativeTime(lastAt),
    unreadCount: 0,
  }
}

function buildRoomTitle(room: ChatRoomType, profile: UserProfile | undefined, currentUserId: string) {
  if (profile?.nickname?.trim()) return profile.nickname.trim()

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
