import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Search, X, MoreVertical, Pin, PinOff, LogOut, Flag } from 'lucide-react'
import { useSearchParams } from 'react-router-dom'
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
import ReportSheet from '../report/ReportSheet'
import Avatar from '../../shared/ui/Avatar'
import { testAvatarPhoto } from '../../shared/lib/userDisplay'
import { useChatRealtime } from './ChatRealtimeContext'
import { enablePush } from '../../shared/push/push'
import { readChatCache, writeChatCache } from './chatCache'

export default function ChatPage() {
  const { user } = useAuth()
  // 連線與未讀數都住在 App 層的 Provider，切分頁不會斷。
  const { unreadByRoom, joinRooms, setActiveRoom, subscribe, forgetRoom } = useChatRealtime()
  // 切分頁會讓本元件被 Router 銷毀，state 全歸零。用模組層級快取當初始值，
  // 切回來先把上次的內容放上畫面，再於背景刷新（見 chatCache.ts）。
  const restored = readChatCache(user?.userId)
  const [rooms, setRooms] = useState<ChatRoomType[]>(restored?.rooms ?? [])
  // 開著哪間房直接由網址決定，不另外存一份 state。
  //
  // 本來是 useState(searchParams.get('room'))，只在掛載時讀一次。那會漏掉
  // 「App 已經停在聊天頁時點推播通知」——元件不重新掛載，?room= 就被忽略了。
  // 由網址驅動還順便拿到：重新整理不會掉出房間、網址可以直接分享。
  const [searchParams, setSearchParams] = useSearchParams()
  const activeRoomId = searchParams.get('room')
  // 用 replace 不累積瀏覽歷史，維持改動前的返回行為
  const setActiveRoomId = useCallback(
    (roomId: string | null) => setSearchParams(roomId ? { room: roomId } : {}, { replace: true }),
    [setSearchParams],
  )
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
  // 列表頁每一列的 ⋮ 選單：同時只開一個
  const [menuRoomId, setMenuRoomId] = useState<string | null>(null)
  // 離開類動作的二次確認，與房內用同一套措辭與樣式
  const [confirm, setConfirm] = useState<{ title: string; desc: string; onYes: () => void } | null>(null)
  // 列表頁的檢舉：選完原因真的送出之後，才補記房號給後台
  const [listReport, setListReport] = useState<{ roomId: string; userId: string } | null>(null)
  const listReportedRef = useRef(false)
  // 操作失敗時給一句話。沒有提示的話使用者分不出是沒點到還是真的失敗了。
  const [toast, setToast] = useState('')
  // 檢舉已成立、因此凍結的房間。觸發點是後台審核認定成立，不是送出檢舉——
  // 送出後到審核之前兩人照常聊天。檢舉的對象是人，所以同一個人的每一間房一起算。
  const [reportedRooms, setReportedRooms] = useState<Set<string>>(() => new Set())
  // toast 自己消失。房內用的是同一套（ChatRoom.tsx 的 toastTimer）。
  useEffect(() => {
    if (!toast) return
    const timer = setTimeout(() => setToast(''), 3000)
    return () => clearTimeout(timer)
  }, [toast])

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


  // 訊息處理器每次 render 都是新的函式，但行為固定（只讀 ref、只呼叫 setState），
  // 所以透過 ref 轉接，訂閱本身只做一次。
  const applyRef = useRef(applyIncomingMessage)
  useEffect(() => { applyRef.current = applyIncomingMessage })

  useEffect(() => subscribe({
    onMessageReceived: (message) => applyRef.current(message),
    onMessageRecalled: (message) => applyRef.current(message),
  }), [subscribe])

  // 房間清單變了就重新加入群組。連線還沒好也沒關係，Provider 會在連上與重連後重放。
  useEffect(() => {
    joinRooms(rooms.map((room) => room.id))
  }, [rooms, joinRooms])

  // 開著哪間房由 Provider 決定未讀要不要加——人正在看的那間不該亮紅點。
  // 進房同時把這間標成已讀。
  useEffect(() => {
    setActiveRoom(activeRoomId)
    return () => setActiveRoom(null)
  }, [activeRoomId, setActiveRoom])

  // 鎖屏或切到別的 App 期間 SignalR 會斷，那段時間的訊息不會被推過來。
  // 回到前景時補抓一次——Provider 這時會把房間標成已讀，不補的話就變成
  // 「紅點消失了，但訊息根本沒出現在畫面上」。
  useEffect(() => {
    const onVisible = () => {
      if (document.visibilityState !== 'visible') return
      const roomId = activeRoomIdRef.current
      if (roomId) void loadMessages(roomId)
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => document.removeEventListener('visibilitychange', onVisible)
  }, [])

  // 推播權限在「第一次真的進到某個對話」時才問。
  // Android 13 起連續拒絕兩次之後系統就永遠不再跳視窗，所以啟動時問是浪費
  // 唯一的機會——那時使用者還不知道這個 App 會通知他什麼。
  // 人已經在對話裡、正等著對方回覆，理由才自明。
  useEffect(() => {
    if (!activeRoomId) return
    void enablePush()
  }, [activeRoomId])

  useEffect(() => {
    if (!activeRoomId) return
    // 秒開：先用 loadRooms already 抓好的快取立即上畫面，再背景抓最新覆蓋。
    setMessages(roomMessagesRef.current[activeRoomId] ?? [])
    loadMessages(activeRoomId)

  }, [activeRoomId])

  // 完整 profile 只有 peek sheet 與房內抬頭用得到，所以進房才抓、只抓這一位。
  // 相依要包含 rooms：從配對慶祝畫面用 /chat?room=xxx 直接進房時，
  // 這個 effect 第一次跑的當下 rooms 還是空的（loadRooms 還沒回來），
  // 找不到對方是誰。等列表到位再跑一次才補得到，否則抬頭會一直停在
  // 退場值「同頻對話 xxxx」。
  useEffect(() => {
    if (!activeRoomId) return
    const room = rooms.find((r) => r.id === activeRoomId)
    const peerId = room?.userIds.find((id) => id !== user?.userId)
    if (!peerId || profilesRef.current[peerId]) return
    getProfile(peerId)
      .then((res) => setProfiles((prev) => ({ ...prev, [peerId]: res.data })))
      .catch(() => {})
  }, [activeRoomId, rooms, user?.userId])

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
    setReportedRooms(new Set(overviewRes.data.filter((item) => item.reported).map((item) => item.room.id)))
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

  async function updateRoomSetting(
    roomId: string,
    patch: { noteName?: string | null; pinned?: boolean; muted?: boolean },
  ) {
    const res = await updateChatSetting(roomId, patch)
    setSettings((prev) => ({ ...prev, [roomId]: res.data }))
  }

  async function updateSetting(patch: { noteName?: string | null; pinned?: boolean; muted?: boolean }) {
    if (!activeRoomId) return
    await updateRoomSetting(activeRoomId, patch)
  }

  /** 離開一間房。從列表的 ⋮ 或房內選單都走這裡。 */
  async function leaveRoom(roomId: string) {
    await leaveChatRoom(roomId)
    // 後端把你移出成員，但 SignalR 的房間群組綁在連線上、不會自己掉；
    // 未讀數也要一起清，否則底部紅點會留下一個點不進去的幽靈數字。
    forgetRoom(roomId)
    if (activeRoomId === roomId) setActiveRoomId(null)
    await loadRooms()
  }

  async function leaveActiveRoom() {
    if (!activeRoomId) return
    await leaveRoom(activeRoomId)
  }

  /**
   * 列表頁 ⋮ 的動作。
   * 釘選是可逆的，直接做；離開與檢舉不可逆，先問過。
   * 檢舉走 ReportSheet 選原因。送出後畫面不變，兩人照常聊天，要等後台認定
   * 成立那間房才唯讀。成立後也不代為離開——使用者常常還要回頭看對方說過
   * 什麼，而且對話憑空消失會讓人以為自己誤觸。
   */
  function handleRoomAction(summary: ChatSummary, action: 'pin' | 'leave' | 'report') {
    setMenuRoomId(null)

    if (action === 'pin') {
      void updateRoomSetting(summary.room.id, { pinned: !summary.pinned })
        .catch(() => setToast(summary.pinned ? '取消釘選失敗，請再試一次' : '釘選失敗，請再試一次'))
      return
    }

    if (action === 'leave') {
      setConfirm({
        title: '離開聊天室',
        // 這個動作實際上是把你移出房間，對話與對方都不會再出現，
        // 不是「只是從列表隱藏」。文案要講清楚，不然使用者會以為還救得回來。
        desc: `離開後就看不到與「${summary.title}」的對話了，而且無法復原。`,
        onYes: () => {
          setConfirm(null)
          void leaveRoom(summary.room.id).catch(() => setToast('離開失敗，請再試一次'))
        },
      })
      return
    }

    setConfirm({
      title: '檢舉對方',
      desc: '接著會請你選擇原因。我們會盡快查看，處理完成會通知你；在那之前你們仍然可以繼續對話。',
      onYes: () => {
        setConfirm(null)
        listReportedRef.current = false
        setListReport({ roomId: summary.room.id, userId: summary.otherUserId })
      },
    })
  }

  async function blockActiveRoom() {
    if (!activeRoomId) return
    await blockChatUser(activeRoomId)
    setActiveRoomId(null)
    await loadRooms()
  }

  async function reportActiveRoom() {
    if (!activeRoomId) return
    // ReportSheet 已經送出檢舉本體（POST /api/reports），這一發是補記房號，
    // 讓後台審核時看得到是哪一段對話。失敗不擋——檢舉本身已經成立了。
    await reportChatUser(activeRoomId).catch(() => {})
    // 停在房裡：重抓之後這間房會變成唯讀，底部直接告訴使用者下一步。
    await loadRooms()
  }

  const activeRoom = rooms.find((room) => room.id === activeRoomId) ?? null
  const roomSummaries = useMemo(
    () => rooms
      .map((room) => {
        const base = buildRoomSummary(room, roomMessages[room.id] ?? [], peers, user?.userId ?? '')
        const setting = settings[room.id]
        return {
          ...base,
          title: setting?.noteName?.trim() || base.title,
          pinned: !!setting?.pinned,
          pinnedAt: setting?.pinnedAt ? new Date(setting.pinnedAt) : null,
          unreadCount: unreadByRoom[room.id] ?? 0,
          reported: reportedRooms.has(room.id),
        }
      })
      .sort((a, b) => {
        // 釘選永遠壓過未讀：有新訊息的一般對話也不能插到釘選的前面
        if (a.pinned !== b.pinned) return a.pinned ? -1 : 1
        // 同一群之內，有未讀的排前面
        const aUnread = a.unreadCount > 0
        const bUnread = b.unreadCount > 0
        if (aUnread !== bUnread) return aUnread ? -1 : 1
        // 釘選區依釘選時間（先釘的在上，順序穩定）；一般區依最後訊息新到舊
        if (a.pinned) return (a.pinnedAt?.getTime() ?? 0) - (b.pinnedAt?.getTime() ?? 0)
        return b.lastAt.getTime() - a.lastAt.getTime()
      }),
    [rooms, roomMessages, peers, settings, unreadByRoom, user?.userId],
  )
  // 不分組，一個平的列表。分組會跟排序打架：釘一個舊對話，它只會在自己那組裡
  // 浮起來，仍被「今天聊起的」整組壓在下面，看起來等於沒釘。
  // 釘選狀態改用名字前的圖示標示。

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
        reported={reportedRooms.has(activeRoom.id)}
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
                  <ChatCard
                    key={summary.room.id}
                    summary={summary}
                    onOpen={setActiveRoomId}
                    highlight={query}
                    menuOpen={menuRoomId === summary.room.id}
                    onToggleMenu={setMenuRoomId}
                    onAction={handleRoomAction}
                  />
                ))}
              </div>
            ) : (
              <div className="chat-search-empty">
                <p>找不到符合的對話</p>
                <p>換個關鍵字試試</p>
              </div>
            )
          ) : (
            <div className="chat-card-list">
              {roomSummaries.map((summary) => (
                <ChatCard
                  key={summary.room.id}
                  summary={summary}
                  onOpen={setActiveRoomId}
                  menuOpen={menuRoomId === summary.room.id}
                  onToggleMenu={setMenuRoomId}
                  onAction={handleRoomAction}
                />
              ))}
            </div>
          )}
        </section>

        {/* 選單開著時蓋一層透明遮罩。它會吃掉捲動手勢，所以捲動＝關閉選單，
            否則使用者會以為畫面卡住了。 */}
        {menuRoomId && (
          <div
            className="chat-menu-mask"
            onClick={() => setMenuRoomId(null)}
            onWheel={() => setMenuRoomId(null)}
            onTouchMove={() => setMenuRoomId(null)}
          />
        )}

        {toast && <div className="cr-toast" role="status">{toast}</div>}

        {confirm && (
          <div className="cr-confirm-mask" onClick={() => setConfirm(null)}>
            <div className="cr-confirm" role="alertdialog" onClick={(e) => e.stopPropagation()}>
              <div className="cr-confirm-title">{confirm.title}</div>
              <div className="cr-confirm-desc">{confirm.desc}</div>
              <div className="cr-confirm-actions">
                <button className="cr-confirm-cancel" type="button" onClick={() => setConfirm(null)}>取消</button>
                <button className="cr-confirm-ok danger" type="button" onClick={confirm.onYes}>確認</button>
              </div>
            </div>
          </div>
        )}

        {/* 真的送出才補記房號；中途取消什麼都不該發生 */}
        {listReport && (
          <ReportSheet
            targetType="USER"
            targetId={listReport.userId}
            title="檢舉對方"
            onClose={() => {
              const roomId = listReport.roomId
              setListReport(null)
              if (!listReportedRef.current) return
              listReportedRef.current = false
              // 送出檢舉之後畫面不該有任何變化：兩人照常聊天，要等後台認定成立
              // 那間房才會變成唯讀（並且會發通知）。這一發是補記房號，讓後台
              // 審核時看得到是哪一段對話。
              void reportChatUser(roomId).catch(() => {}).then(() => loadRooms())
            }}
            onDone={() => { listReportedRef.current = true }}
          />
        )}
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
              {/* 版面住在 .chat-card-open（真卡片的點擊區），骨架少了這層
                  就會失去 padding 與 grid，列高比真卡片矮一截，資料回來時畫面會跳 */}
              <span className="chat-card-open">
                <span className="chat-avatar skeleton-block" />
                <span className="chat-card-main">
                  <span className="skeleton-line skeleton-line-name" />
                  <span className="skeleton-line skeleton-line-preview" />
                </span>
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
  pinnedAt: Date | null
  reported: boolean
}

type RoomAction = 'pin' | 'leave' | 'report'

type CardProps = {
  summary: ChatSummary
  onOpen: (roomId: string) => void
  highlight?: string
  menuOpen: boolean
  onToggleMenu: (roomId: string | null) => void
  onAction: (summary: ChatSummary, action: RoomAction) => void
}

/** 選單展開後的大約高度（三個項目＋分隔線＋內距），用來決定往上還是往下開。 */
const MENU_HEIGHT = 164

function ChatCard({ summary, onOpen, highlight, menuOpen, onToggleMenu, onAction }: CardProps) {
  const cardRef = useRef<HTMLDivElement>(null)
  const [dropUp, setDropUp] = useState(false)

  function toggleMenu() {
    if (!menuOpen) {
      // 下方空間不夠就往上開——不然最後幾列的「離開」「檢舉」永遠點不到。
      //
      // 可用空間要取「真的看得到的下緣」，有兩個東西會吃掉它：
      // 捲動容器的 rect 是整份內容的高度（這份列表量到 2380px），不是可視區；
      // 底部導覽列又會再蓋掉最後 64px。先前只看捲動容器的下緣，算出來永遠
      // 有上千 px 的空間，往上開因此一次都沒觸發過，最後一列的選單整個開在
      // 畫面外。
      const card = cardRef.current
      const scroller = card?.closest('.chat-list-scroll')
      const navTop = document.querySelector('.bottom-nav')?.getBoundingClientRect().top ?? window.innerHeight
      const limit = Math.min(
        scroller?.getBoundingClientRect().bottom ?? window.innerHeight,
        window.innerHeight,
        navTop,
      )
      const cardBottom = card?.getBoundingClientRect().bottom ?? 0
      setDropUp(limit - cardBottom < MENU_HEIGHT)
    }
    onToggleMenu(menuOpen ? null : summary.room.id)
  }

  // 外層從 button 改成 div：裡面要再放一顆 ⋮ 按鈕，互動元素不能巢狀
  // （巢狀的點擊目標在螢幕閱讀器與觸控裝置上行為都不可預期）。
  return (
    <div className={`chat-list-card${menuOpen ? ' menu-open' : ''}`} ref={cardRef}>
      <button className="chat-card-open" type="button" onClick={() => onOpen(summary.room.id)}>
        <Avatar photo={summary.avatarPhoto} seed={summary.otherUserId} className="chat-avatar" />
        <span className="chat-card-main">
          <span className="chat-card-name-row">
            <strong>
              {summary.pinned && <Pin size={12} strokeWidth={2.2} className="chat-card-pin" aria-label="已釘選" />}
              {highlight ? <HighlightName name={summary.title} query={highlight} /> : summary.title}
            </strong>
            <time>{summary.timeLabel}</time>
          </span>
          <span className={`chat-card-preview${summary.unreadCount > 0 ? ' unread' : ''}`}>
            {/* 已處置的房間點進去只剩「退出聊天室」，列表上就先講，
                不然使用者得一間一間點開才知道是哪一間 */}
            {summary.reported && <span className="chat-card-tag">已處置</span>}
            {summary.preview}
          </span>
        </span>
        {summary.unreadCount > 0 && (
          <span className="chat-unread-badge">{summary.unreadCount > 99 ? '99+' : summary.unreadCount}</span>
        )}
      </button>

      <button
        className="chat-card-more"
        type="button"
        aria-label={`${summary.title} 的更多操作`}
        aria-expanded={menuOpen}
        onClick={toggleMenu}
      >
        <MoreVertical size={18} strokeWidth={2} />
      </button>

      {menuOpen && (
        <div className={`chat-card-menu${dropUp ? ' drop-up' : ''}`} role="menu">
          <button className="cr-menu-item" type="button" role="menuitem" onClick={() => onAction(summary, 'pin')}>
            {summary.pinned
              ? <PinOff size={18} strokeWidth={1.8} className="cr-menu-ic" />
              : <Pin size={18} strokeWidth={1.8} className="cr-menu-ic" />}
            <span>{summary.pinned ? '取消釘選' : '釘選對話'}</span>
          </button>
          <div className="cr-menu-divider" />
          <button className="cr-menu-item danger" type="button" role="menuitem" onClick={() => onAction(summary, 'leave')}>
            <LogOut size={18} strokeWidth={1.8} className="cr-menu-ic" /><span>離開聊天室</span>
          </button>
          {/* 已經處置過就不再給這一項：再送一次對審核沒有幫助，
              而使用者按下去只會看到同一張面板，像是上一次沒成功 */}
          {!summary.reported && (
            <button className="cr-menu-item danger" type="button" role="menuitem" onClick={() => onAction(summary, 'report')}>
              <Flag size={18} strokeWidth={1.8} className="cr-menu-ic" /><span>檢舉使用者</span>
            </button>
          )}
        </div>
      )}
    </div>
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
    pinnedAt: null,
    // 這三個預設值由呼叫端用 settings / unreadByRoom / reportedRooms 覆寫
    reported: false,
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
