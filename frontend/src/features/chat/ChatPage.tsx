import { useEffect, useMemo, useRef, useState } from 'react'
import { Search, X } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import {
  getChatOverview, getMessages, sendMessage, recallMessage,
  getMemberSettings, updateChatSetting, leaveChatRoom, blockChatUser, reportChatUser,
  type QuoteInfo,
} from './api'
import type { ChatMessage, ChatRoom as ChatRoomType, ChatMemberSetting, ChatPeer } from './types'
import { getProfile } from '../profile/api'
import type { UserProfile } from '../profile/types'
import ChatRoom from './ChatRoom'
import Avatar from '../../shared/ui/Avatar'
import { testAvatarPhoto } from '../../shared/lib/userDisplay'
import { createChatConnection, joinChatRooms } from './realtime'
import { readChatCache, writeChatCache } from './chatCache'

export default function ChatPage() {
  const { user } = useAuth()
  // 切分頁會讓本元件被 Router 銷毀，state 全歸零。用模組層級快取當初始值，
  // 切回來先把上次的內容放上畫面，再於背景刷新（見 chatCache.ts）。
  const restored = readChatCache(user?.userId)
  const [rooms, setRooms] = useState<ChatRoomType[]>(restored?.rooms ?? [])
  const [activeRoomId, setActiveRoomId] = useState<string | null>(null)
  const [roomMessages, setRoomMessages] = useState<Record<string, ChatMessage[]>>(restored?.roomMessages ?? {})
  // 列表只需要暱稱與頭像，由 overview 一次帶回；完整 profile 進房才抓
  const [peers, setPeers] = useState<Record<string, ChatPeer>>(restored?.peers ?? {})
  const [profiles, setProfiles] = useState<Record<string, UserProfile>>(restored?.profiles ?? {})
  const [settings, setSettings] = useState<Record<string, ChatMemberSetting>>(restored?.settings ?? {})
  const [messages, setMessages] = useState<ChatMessage[]>([])
  // 載入中與「真的沒有聊天室」是兩回事。先前沒有這個旗標，使用者在等待的
  // 那幾秒看到的是「還沒有聊天室」——等於 App 明確告訴他沒有對話。
  const [loaded, setLoaded] = useState(restored != null)
  const [draft, setDraft] = useState('')
  const [searchOpen, setSearchOpen] = useState(false)
  const [search, setSearch] = useState('')
  const connectionRef = useRef<ReturnType<typeof createChatConnection> | null>(null)
  const roomsRef = useRef<ChatRoomType[]>(restored?.rooms ?? [])
  const activeRoomIdRef = useRef<string | null>(null)
  // 開房時要立即讀到已快取的訊息，但不能把 roomMessages 放進 effect deps（會無限重跑）。
  // 初始值同樣取還原資料，否則切回分頁後第一次開房仍會落空。
  const roomMessagesRef = useRef<Record<string, ChatMessage[]>>(restored?.roomMessages ?? {})
  // 同理：進房時要判斷「這位的完整 profile 抓過沒」，但不能放進 deps
  const profilesRef = useRef<Record<string, UserProfile>>(restored?.profiles ?? {})

  useEffect(() => { profilesRef.current = profiles }, [profiles])

  useEffect(() => {
    activeRoomIdRef.current = activeRoomId
  }, [activeRoomId])

  useEffect(() => {
    roomsRef.current = rooms
  }, [rooms])

  useEffect(() => {
    roomMessagesRef.current = roomMessages
  }, [roomMessages])

  // 把最新內容留在模組層級，切走再切回時可以直接接上
  useEffect(() => {
    writeChatCache(user?.userId, { rooms, roomMessages, peers, profiles, settings })
  }, [user?.userId, rooms, roomMessages, peers, profiles, settings])

  useEffect(() => {
    loadRooms().catch(() => {}).finally(() => setLoaded(true))
  }, [])

  useEffect(() => {
    if (!user) return

    const connection = createChatConnection({
      onMessageReceived: (message) => {
        applyIncomingMessage(message)
      },
      onMessageRecalled: (message) => {
        applyIncomingMessage(message)
      },
    })
    connectionRef.current = connection

    // StrictMode 開發模式會 mount → unmount → mount。若在 start() 還在 negotiate 時就 stop()，
    // 連線會以「The connection was stopped during negotiation」失敗且不會重連 → 收不到即時訊息。
    // 因此 cleanup 不直接 stop，而是等 start() 這輪走完再收，並用 cancelled 擋掉過期的後續動作。
    let cancelled = false

    const startPromise = connection
      .start()
      .then(() => {
        if (cancelled) return
        return joinChatRooms(connection, roomsRef.current.map((room) => room.id))
      })
      .catch(() => {
        if (!cancelled) connectionRef.current = null
      })

    connection.onreconnected(() => {
      joinChatRooms(connection, roomsRef.current.map((room) => room.id)).catch(() => {})
    })

    return () => {
      cancelled = true
      connectionRef.current = null
      startPromise.finally(() => connection.stop().catch(() => {}))
    }
  }, [user?.userId])

  useEffect(() => {
    if (!connectionRef.current) return
    joinChatRooms(connectionRef.current, rooms.map((room) => room.id)).catch(() => {})
  }, [rooms])

  useEffect(() => {
    if (!activeRoomId) return
    // 秒開：先用 loadRooms already 抓好的快取立即上畫面，再背景抓最新覆蓋。
    setMessages(roomMessagesRef.current[activeRoomId] ?? [])
    loadMessages(activeRoomId)

    // 完整 profile 只有 peek sheet 用得到，所以進房才抓、而且只抓這一位。
    // 列表本身用 overview 帶回的 peers（暱稱＋頭像）就夠了。
    const room = roomsRef.current.find((r) => r.id === activeRoomId)
    const peerId = room?.userIds.find((id) => id !== user?.userId)
    if (peerId && !profilesRef.current[peerId]) {
      getProfile(peerId)
        .then((res) => setProfiles((prev) => ({ ...prev, [peerId]: res.data })))
        .catch(() => {})
    }
  }, [activeRoomId])

  async function loadRooms() {
    if (!user) return

    // 列表所需的一切由 overview 一次帶回：房間、對方暱稱與頭像、最後一則訊息。
    //
    // 先前是兩波：房間清單 → 再對每間房打一次 /messages、每個對方打一次
    // /profile。30 間房就是 60 幾個請求，而瀏覽器對同一來源只開 6 條連線，
    // 實測單一請求 54-331ms、擠在一起時中位數變成 4,353ms。
    // 設定端點未就緒時不影響列表，故單獨吞掉錯誤。
    const [overviewRes, settingsRes] = await Promise.all([
      getChatOverview(user.userId),
      getMemberSettings(user.userId).catch(() => null),
    ])

    setRooms(overviewRes.data.map((item) => item.room))
    if (settingsRes) {
      setSettings(Object.fromEntries(settingsRes.data.map((s) => [s.chatRoomId, s])))
    }

    const nextPeers: Record<string, ChatPeer> = {}
    const nextMessages: Record<string, ChatMessage[]> = {}
    for (const item of overviewRes.data) {
      if (item.peerUserId) {
        nextPeers[item.peerUserId] = { nickname: item.peerNickname, photoUrl: item.peerPhotoUrl }
      }
      // 列表只要最後一則；進房時 loadMessages 會補上完整的 50 則。
      // 已經進過的房間保留原本的完整訊息，不要被這一則蓋掉。
      const existing = roomMessagesRef.current[item.room.id]
      nextMessages[item.room.id] = existing && existing.length > 1
        ? existing
        : (item.lastMessage ? [item.lastMessage] : [])
    }
    setPeers(nextPeers)
    setRoomMessages(nextMessages)
  }

  async function loadMessages(roomId: string) {
    const response = await getMessages(roomId)
    setRoomMessages((prev) => ({ ...prev, [roomId]: response.data }))
    // 抓取期間使用者可能已切走，別把別房的訊息蓋到當前畫面
    if (activeRoomIdRef.current !== roomId) return
    setMessages((prev) => {
      // 伺服器不知道本地還在送出/送失敗的訊息，覆蓋前要留下來
      const pending = prev.filter((m) => m.status === 'sending' || m.status === 'failed')
      return [...response.data, ...pending]
    })
  }

  async function submitContent(content: string, quote?: QuoteInfo) {
    if (!activeRoomId || !content.trim() || !user) return
    const roomId = activeRoomId
    const tempId = `temp_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`

    // 秒傳：先把訊息放上畫面，不等伺服器往返。
    applyIncomingMessage({
      id: tempId,
      chatRoomId: roomId,
      senderId: user.userId,
      content: content.trim(),
      createdAt: new Date().toISOString(),
      status: 'sending',
      ...quote,
    })

    try {
      const response = await sendMessage(roomId, content, quote)
      // 換成伺服器版本（拿到真 id / 時間）。SignalR 廣播若先到也無妨，upsert 依 id 收斂。
      replaceMessage(roomId, tempId, response.data)
    } catch {
      markMessageFailed(roomId, tempId)
    }
  }

  async function recallMsg(messageId: string) {
    if (!activeRoomId) return
    const response = await recallMessage(activeRoomId, messageId)
    applyIncomingMessage(response.data)
  }

  // 用伺服器回來的正式訊息取代樂觀訊息
  function replaceMessage(roomId: string, tempId: string, real: ChatMessage) {
    const swap = (list: ChatMessage[]) => upsertMessage(list.filter((m) => m.id !== tempId), real)
    setRoomMessages((prev) => ({ ...prev, [roomId]: swap(prev[roomId] ?? []) }))
    if (activeRoomIdRef.current === roomId) setMessages((prev) => swap(prev))
  }

  // 送出失敗：保留訊息內容並標記，讓使用者知道沒送出去
  function markMessageFailed(roomId: string, tempId: string) {
    const mark = (list: ChatMessage[]) =>
      list.map((m) => (m.id === tempId ? { ...m, status: 'failed' as const } : m))
    setRoomMessages((prev) => ({ ...prev, [roomId]: mark(prev[roomId] ?? []) }))
    if (activeRoomIdRef.current === roomId) setMessages((prev) => mark(prev))
  }

  function applyIncomingMessage(message: ChatMessage) {
    const roomExists = roomsRef.current.some((room) => room.id === message.chatRoomId)
    if (!roomExists) {
      loadRooms()
      return
    }

    setRoomMessages((prev) => ({
      ...prev,
      [message.chatRoomId]: upsertMessage(prev[message.chatRoomId] ?? [], message),
    }))

    if (activeRoomIdRef.current === message.chatRoomId) {
      setMessages((prev) => upsertMessage(prev, message))
    }
  }

  async function updateSetting(patch: { noteName?: string | null; pinned?: boolean; muted?: boolean }) {
    if (!activeRoomId) return
    const res = await updateChatSetting(activeRoomId, patch)
    setSettings((prev) => ({ ...prev, [activeRoomId]: res.data }))
  }

  async function leaveActiveRoom() {
    if (!activeRoomId) return
    await leaveChatRoom(activeRoomId)
    setActiveRoomId(null)
    await loadRooms()
  }

  async function blockActiveRoom() {
    if (!activeRoomId) return
    await blockChatUser(activeRoomId)
    setActiveRoomId(null)
    await loadRooms()
  }

  async function reportActiveRoom() {
    if (!activeRoomId) return
    await reportChatUser(activeRoomId)
    setActiveRoomId(null)
    await loadRooms()
  }

  const activeRoom = rooms.find((room) => room.id === activeRoomId) ?? null
  const roomSummaries = useMemo(
    () => rooms
      .map((room) => {
        const base = buildRoomSummary(room, roomMessages[room.id] ?? [], peers, user?.userId ?? '')
        const setting = settings[room.id]
        return { ...base, title: setting?.noteName?.trim() || base.title, pinned: !!setting?.pinned }
      })
      // 釘選優先，其次依最後訊息時間新到舊
      .sort((a, b) => (b.pinned ? 1 : 0) - (a.pinned ? 1 : 0) || b.lastAt.getTime() - a.lastAt.getTime()),
    [rooms, roomMessages, profiles, settings, user?.userId],
  )
  const todayRooms = roomSummaries.filter((summary) => isToday(summary.room.createdAt))
  const previousRooms = roomSummaries.filter((summary) => !isToday(summary.room.createdAt))

  // 名字搜尋：連續片段(contains)、不分大小寫，僅比對對話對象名字
  const query = search.trim().toLowerCase()
  const searchResults = useMemo(
    () => (query ? roomSummaries.filter((summary) => summary.title.toLowerCase().includes(query)) : roomSummaries),
    [roomSummaries, query],
  )

  function closeSearch() {
    setSearch('')
    setSearchOpen(false)
  }

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
        onSendContent={submitContent}
        onRecall={recallMsg}
        setting={settings[activeRoom.id]}
        onUpdateSetting={updateSetting}
        onLeave={leaveActiveRoom}
        onBlock={blockActiveRoom}
        onReport={reportActiveRoom}
        onBack={() => setActiveRoomId(null)}
      />
    )
  }

  if (rooms.length > 0) {
    return (
      <div className="page chat-list-page">
        <header className="chat-list-header">
          <h1>聊天</h1>
          {searchOpen ? (
            <div className="chat-search-box">
              <Search size={14} className="chat-search-icon" strokeWidth={2} />
              <input
                className="chat-search-input"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="搜尋名字"
                autoFocus
              />
              <button className="chat-search-clear" type="button" onClick={closeSearch} aria-label="關閉搜尋">
                <X size={14} strokeWidth={2} />
              </button>
            </div>
          ) : (
            <button className="chat-search-btn" type="button" onClick={() => setSearchOpen(true)} aria-label="搜尋名字">
              <Search size={18} strokeWidth={2} />
            </button>
          )}
        </header>
        <section className="chat-list-scroll" aria-label="聊天列表">
          {searchOpen ? (
            searchResults.length > 0 ? (
              <div className="chat-card-list chat-search-results">
                {searchResults.map((summary) => (
                  <ChatCard key={summary.room.id} summary={summary} onOpen={setActiveRoomId} highlight={query} />
                ))}
              </div>
            ) : (
              <div className="chat-search-empty">
                <p>找不到符合的對話</p>
                <p>換個關鍵字試試</p>
              </div>
            )
          ) : (
            <>
              <ChatSection title="今天聊起的" rooms={todayRooms} onOpen={setActiveRoomId} />
              <ChatSection title="先前的對話" rooms={previousRooms} onOpen={setActiveRoomId} />
            </>
          )}
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
      {loaded ? (
        <section className="panel empty-state">還沒有聊天室。雙方都按「想聊聊」後，這裡會出現對話。</section>
      ) : (
        <div className="chat-card-list">
          {[0, 1, 2, 3].map((i) => (
            <div key={i} className="chat-list-card chat-card-skeleton" aria-hidden="true">
              <span className="chat-avatar skeleton-block" />
              <span className="chat-card-main">
                <span className="skeleton-line skeleton-line-name" />
                <span className="skeleton-line skeleton-line-preview" />
              </span>
            </div>
          ))}
        </div>
      )}
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
  pinned: boolean
}

function ChatSection({ title, rooms, onOpen }: { title: string; rooms: ChatSummary[]; onOpen: (roomId: string) => void }) {
  if (rooms.length === 0) return null

  return (
    <section className="chat-section">
      <h2>{title}</h2>
      <div className="chat-card-list">
        {rooms.map((summary) => (
          <ChatCard key={summary.room.id} summary={summary} onOpen={onOpen} />
        ))}
      </div>
    </section>
  )
}

function ChatCard({ summary, onOpen, highlight }: { summary: ChatSummary; onOpen: (roomId: string) => void; highlight?: string }) {
  return (
    <button className="chat-list-card" type="button" onClick={() => onOpen(summary.room.id)}>
      <Avatar photo={summary.avatarPhoto} seed={summary.otherUserId} className="chat-avatar" />
      <span className="chat-card-main">
        <span className="chat-card-name-row">
          <strong>{highlight ? <HighlightName name={summary.title} query={highlight} /> : summary.title}</strong>
          <time>{summary.timeLabel}</time>
        </span>
        <span className={`chat-card-preview${summary.unreadCount > 0 ? ' unread' : ''}`}>{summary.preview}</span>
      </span>
      {summary.unreadCount > 0 && (
        <span className="chat-unread-badge">{summary.unreadCount > 99 ? '99+' : summary.unreadCount}</span>
      )}
    </button>
  )
}

// 名字中命中片段高亮（query 已小寫）
function HighlightName({ name, query }: { name: string; query: string }) {
  const idx = name.toLowerCase().indexOf(query)
  if (idx === -1) return <>{name}</>
  return (
    <>
      {name.slice(0, idx)}
      <mark className="chat-search-hl">{name.slice(idx, idx + query.length)}</mark>
      {name.slice(idx + query.length)}
    </>
  )
}

function buildRoomSummary(room: ChatRoomType, messages: ChatMessage[], peers: Record<string, ChatPeer>, currentUserId: string): ChatSummary {
  const lastMessage = messages[messages.length - 1]
  const otherUserId = room.userIds.find((id) => id !== currentUserId) ?? room.id
  const peer = peers[otherUserId]
  const title = buildRoomTitle(room, peer, currentUserId)
  const lastAt = new Date(lastMessage?.createdAt ?? room.createdAt)

  return {
    room,
    title,
    otherUserId,
    // 正式環境用帳號 photoDataUrls[0]；測試先給寫死照片，失效時 Avatar 退回動物頭像
    avatarPhoto: peer?.photoUrl ?? testAvatarPhoto(otherUserId),
    preview: formatMessagePreview(lastMessage, currentUserId, title),
    lastAt,
    timeLabel: formatRelativeTime(lastAt),
    unreadCount: 0,
    pinned: false,
  }
}

function buildRoomTitle(room: ChatRoomType, peer: ChatPeer | undefined, currentUserId: string) {
  if (peer?.nickname?.trim()) return peer.nickname.trim()

  const otherUserId = room.userIds.find((id) => id !== currentUserId)
  if (otherUserId) {
    return `同頻對話 ${otherUserId.slice(-4)}`
  }

  if (room.sourceType === 'TASK') return '任務對話'
  if (room.sourceType === 'RANT') return '樹洞對話'
  return '同頻對話'
}

function upsertMessage(messages: ChatMessage[], message: ChatMessage) {
  const exists = messages.some((item) => item.id === message.id)
  const next = exists
    ? messages.map((item) => item.id === message.id ? message : item)
    : [...messages, message]
  return next.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
}

function formatMessagePreview(message: ChatMessage | undefined, currentUserId: string, otherName: string) {
  if (!message) return '還沒開始聊天'
  const content = message.content.trim()
  const who = message.senderId === currentUserId ? '你' : otherName
  if (content === '[sticker]') return `${who}傳了一張貼圖`
  // 圖片訊息內容是 data URL 或圖片網址，預覽不顯示原始字串，改成「XX傳了一張圖片」
  if (content === '[image]' || isImagePreview(content)) {
    return `${who}傳了一張圖片`
  }
  // 語音訊息的內容同樣是一串網址，不折起來的話列表會直接露出
  // https://...supabase.co/storage/... 這種東西。判斷式與 ChatRoom 的
  // isAudioMedia 一致，兩邊要一起改。
  if (content === '[audio]' || isAudioPreview(content)) {
    return `${who}傳了一則語音`
  }
  return content || '還沒開始聊天'
}

function isImagePreview(s: string) {
  return /^data:image\//.test(s) || (/^https?:\/\//.test(s) && /\.(jpe?g|png|webp|gif)(\?|$)/i.test(s))
}

function isAudioPreview(s: string) {
  return /^data:audio\//.test(s) || (/^https?:\/\//.test(s) && /\.(mp3|webm|wav|m4a|ogg)(\?|$)/i.test(s))
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
