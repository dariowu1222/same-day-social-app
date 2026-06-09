import { useEffect, useState } from 'react'
import type { DemoUser } from '../App'
import { getChatRooms, getMessages, sendMessage, type ChatMessage, type ChatRoom as ChatRoomType } from '../api/client'
import ChatRoom from '../components/ChatRoom'

type Props = {
  user: DemoUser
}

export default function ChatPage({ user }: Props) {
  const [rooms, setRooms] = useState<ChatRoomType[]>([])
  const [activeRoomId, setActiveRoomId] = useState<string | null>(null)
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
    setActiveRoomId((current) => current ?? response.data[0]?.id ?? null)
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
  }

  const activeRoom = rooms.find((room) => room.id === activeRoomId) ?? null

  return (
    <div className="page">
      <header className="page-header">
        <p className="eyebrow">聊天</p>
        <h1>慢慢說就好。</h1>
      </header>
      {rooms.length > 0 && (
        <section className="room-tabs">
          {rooms.map((room) => (
            <button key={room.id} className={room.id === activeRoomId ? 'active' : ''} onClick={() => setActiveRoomId(room.id)}>
              {room.sourceType}
            </button>
          ))}
        </section>
      )}
      <ChatRoom room={activeRoom} messages={messages} draft={draft} onDraftChange={setDraft} onSend={submitMessage} />
    </div>
  )
}
