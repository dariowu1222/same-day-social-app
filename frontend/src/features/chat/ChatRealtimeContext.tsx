import {
  createContext, useCallback, useContext, useEffect, useMemo, useRef, useState,
  type ReactNode,
} from 'react'
import * as signalR from '@microsoft/signalr'
import { useAuth } from '../auth/AuthContext'
import { createChatConnection } from './realtime'
import { getChatUnread, markChatRead } from './api'
import type { ChatMessage } from './types'

/**
 * 跨分頁存活的聊天即時連線與未讀數。
 *
 * 連線原本建在 ChatPage 的 effect 裡，切到今日／樹洞／我的就被 Router 銷毀，
 * 人不在聊天頁時完全收不到新訊息——底部的未讀紅點因此不可能會動。
 * 所以把它提到 App 層。
 *
 * 提上來之後失去了一個原本白吃的自癒機制：以前切分頁＝重新掛載＝重建連線，
 * 現在整個 session 只建一次。而 withAutomaticReconnect() 的預設重試序列
 * （0s/2s/10s/30s）用完就永久停止，手機鎖屏超過一分鐘就會耗盡。
 * 所以這裡自己補了 onclose 無限退避重啟，並在重連後重放 JoinRooms 與重抓未讀。
 */

type RoomActivity = { roomId: string; senderId: string }

type MessageHandler = {
  onMessageReceived: (message: ChatMessage) => void
  onMessageRecalled: (message: ChatMessage) => void
}

type ChatRealtimeValue = {
  unreadByRoom: Record<string, number>
  totalUnread: number
  /** 聊天頁拿到房間清單後呼叫；連線還沒好也沒關係，連上會自動重放。 */
  joinRooms: (roomIds: string[]) => void
  /** 目前開著哪一間房，null 代表回到列表或離開聊天頁。 */
  setActiveRoom: (roomId: string | null) => void
  /** 立刻把一間房標成已讀（本地先歸零，再送後端）。 */
  markRead: (roomId: string) => void
  /** 已經離開一間房：清掉它的未讀，並退出它的即時群組。 */
  forgetRoom: (roomId: string) => void
  /** 向伺服器重新校正未讀數。 */
  refreshUnread: () => void
  /** 註冊訊息處理器，回傳取消註冊的函式。 */
  subscribe: (handler: MessageHandler) => () => void
}

const ChatRealtimeContext = createContext<ChatRealtimeValue | null>(null)

/** 重連退避：最長 30 秒，永不放棄。 */
const RETRY_DELAYS = [0, 2000, 5000, 10000, 30000]

/** 人還在房內時，收到新訊息多久後把水位推上去。 */
const MARK_READ_DEBOUNCE_MS = 1500

const EMPTY_UNREAD: Record<string, number> = {}

/** 未讀校正的最短間隔。掛載、連上、重連、回前景可能同時發生。 */
const REFRESH_THROTTLE_MS = 5000

/** 連線活過這麼久才算「穩定」，退避計數才歸零。 */
const STABLE_AFTER_MS = 60000

/** 快照過期被丟掉後，隔多久補打一次。 */
const REFETCH_DELAY_MS = 1200

export function ChatRealtimeProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const userId = user?.userId
  // 未讀數連同「這是誰的」一起存。換帳號時不用手動清一堆狀態——
  // owner 對不上就當成空的，B 不會先看到 A 的紅點。
  const [unread, setUnread] = useState<{ owner?: string; byRoom: Record<string, number> }>({ byRoom: {} })

  const connectionRef = useRef<signalR.HubConnection | null>(null)
  // 連線尚未 Connected 時 joinRooms 會被丟掉，所以房號留在這裡，
  // 連上與重連後都從這裡重放一次。
  const roomIdsRef = useRef<string[]>([])
  const activeRoomRef = useRef<string | null>(null)
  const handlersRef = useRef<Set<MessageHandler>>(new Set())
  const markTimersRef = useRef<Map<string, ReturnType<typeof setTimeout>>>(new Map())
  // 本地每動一次未讀就 +1。伺服器快照回來時若對不上，代表快照已經過時，
  // 整份丟棄而不是蓋掉——否則「剛收到的新訊息」或「剛標的已讀」會被抹掉。
  const seqRef = useRef(0)
  // 校正是「有事發生就順手做一次」，掛載、連上、重連、回前景都會呼叫。
  // 不節流的話這些時機擠在一起會連打好幾次同樣的請求。
  const lastRefreshRef = useRef(0)
  // 快照過期被丟掉之後補打用的計時器
  const refetchTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  const refreshRef = useRef<(options?: { force?: boolean }) => void>(() => {})

  const refreshUnread = useCallback((options?: { force?: boolean }) => {
    if (!userId) return

    const now = Date.now()
    if (!options?.force && now - lastRefreshRef.current < REFRESH_THROTTLE_MS) return
    lastRefreshRef.current = now

    const seq = seqRef.current
    void getChatUnread(userId)
      .then((res) => {
        if (seqRef.current !== seq) {
          // 請求途中本地動過（收到新訊息、標了已讀），這份快照已經過時。
          // 丟掉之後一定要補一次，否則未讀數就永遠停在本地累加的值、
          // 再也不會跟伺服器對齊。
          if (refetchTimerRef.current) clearTimeout(refetchTimerRef.current)
          refetchTimerRef.current = setTimeout(() => refreshRef.current({ force: true }), REFETCH_DELAY_MS)
          return
        }
        setUnread({ owner: userId, byRoom: res.data ?? {} })
      })
      .catch(() => {
        // 校正失敗不處理：本地累加的數字還在，下次事件或回前景時再試
      })
  }, [userId])

  useEffect(() => { refreshRef.current = refreshUnread }, [refreshUnread])

  const markRead = useCallback((roomId: string) => {
    seqRef.current += 1
    setUnread((prev) => {
      if (!prev.byRoom[roomId]) return prev
      const byRoom = { ...prev.byRoom }
      delete byRoom[roomId]
      return { ...prev, byRoom }
    })
    void markChatRead(roomId).catch(() => {})
  }, [])

  /**
   * 離開一間房之後呼叫。
   *
   * 不只是把未讀清掉：SignalR 的房間群組是綁在連線上的，後端把你移出
   * chat_room_users 不會動到它。連線現在跨分頁存活、整個 session 只建一次，
   * 所以不主動退出的話，對方每傳一則訊息仍會送到這台裝置，
   * 而 ChatPage 找不到那間房就會整份重抓 overview——一個停不下來的迴圈。
   */
  const forgetRoom = useCallback((roomId: string) => {
    seqRef.current += 1
    setUnread((prev) => {
      if (!(roomId in prev.byRoom)) return prev
      const byRoom = { ...prev.byRoom }
      delete byRoom[roomId]
      return { ...prev, byRoom }
    })

    roomIdsRef.current = roomIdsRef.current.filter((id) => id !== roomId)

    const timer = markTimersRef.current.get(roomId)
    if (timer) {
      clearTimeout(timer)
      markTimersRef.current.delete(roomId)
    }

    const connection = connectionRef.current
    if (connection?.state === signalR.HubConnectionState.Connected) {
      connection.invoke('LeaveRoom', roomId).catch(() => {})
    }
  }, [])

  const scheduleMarkRead = useCallback((roomId: string) => {
    const timers = markTimersRef.current
    const existing = timers.get(roomId)
    if (existing) clearTimeout(existing)

    timers.set(roomId, setTimeout(() => {
      timers.delete(roomId)
      // 引爆時重新確認：人還在這間房嗎、頁面還看得到嗎？
      // 這兩件事在 1.5 秒內很容易改變（切房、切分頁、鎖屏）。
      if (activeRoomRef.current !== roomId) return
      if (document.visibilityState !== 'visible') return
      markRead(roomId)
    }, MARK_READ_DEBOUNCE_MS))
  }, [markRead])

  const setActiveRoom = useCallback((roomId: string | null) => {
    const previous = activeRoomRef.current
    activeRoomRef.current = roomId
    if (roomId) {
      markRead(roomId)
      return
    }

    // 離開房間。剛才排程中的「標已讀」不能整批丟掉——使用者確實看過那些訊息了，
    // 丟掉的話下一次校正會從伺服器把它們撈回來變成未讀。
    // 「讀完立刻按返回」是最常見的操作，1.5 秒的 debounce 幾乎每次都還沒引爆。
    const timers = markTimersRef.current
    timers.forEach((timer, id) => {
      clearTimeout(timer)
      if (id === previous) markRead(id)
    })
    timers.clear()
  }, [markRead])

  const joinRooms = useCallback((roomIds: string[]) => {
    roomIdsRef.current = roomIds
    const connection = connectionRef.current
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) return
    if (roomIds.length === 0) return
    connection.invoke('JoinRooms', roomIds).catch(() => {})
  }, [])

  const subscribe = useCallback((handler: MessageHandler) => {
    handlersRef.current.add(handler)
    return () => { handlersRef.current.delete(handler) }
  }, [])

  // 未讀數不等 SignalR。Render 冷啟動時 negotiate 可能要幾十秒，
  // 而這支是普通的 HTTP，跟聊天列表同時出發就好。
  useEffect(() => { refreshUnread() }, [refreshUnread])

  // ── 連線本體 ──────────────────────────────────────────
  useEffect(() => {
    if (!userId) return

    const connection = createChatConnection({
      onMessageReceived: (message) => {
        handlersRef.current.forEach((h) => h.onMessageReceived(message))
      },
      onMessageRecalled: (message) => {
        handlersRef.current.forEach((h) => h.onMessageRecalled(message))
      },
    })
    connectionRef.current = connection

    connection.on('roomActivity', (activity: RoomActivity) => {
      // 自己發的訊息不算自己的未讀（後端也擋了一層，這裡再擋一次）
      if (!activity?.roomId || activity.senderId === userId) return

      // 人正開著這間房而且螢幕看得到 → 不亮紅點，改把水位往前推
      if (activity.roomId === activeRoomRef.current && document.visibilityState === 'visible') {
        scheduleMarkRead(activity.roomId)
        return
      }

      seqRef.current += 1
      setUnread((prev) => ({
        owner: userId,
        byRoom: { ...prev.byRoom, [activity.roomId]: (prev.byRoom[activity.roomId] ?? 0) + 1 },
      }))
    })

    // 換帳號時把上一個人殘留的狀態清乾淨。未讀數靠 state 裡的 owner 自動失效，
    // 這些 ref 則要手動歸零，否則會把前一個帳號的房號重放進新的連線。
    roomIdsRef.current = []
    activeRoomRef.current = null
    lastRefreshRef.current = 0

    // cleanup 跑的時候 ref 可能已經換人，所以在這裡抓住當下這一份
    const markTimers = markTimersRef.current

    let stopped = false
    let attempt = 0
    let connectedAt = 0
    let retryTimer: ReturnType<typeof setTimeout> | undefined

    const replay = () => {
      const roomIds = roomIdsRef.current
      if (roomIds.length === 0) return
      connection.invoke('JoinRooms', roomIds).catch(() => {})
    }

    const scheduleRetry = () => {
      if (stopped) return
      // 畫面在背景時不要重連。瀏覽器會節流背景分頁的計時器，SignalR 的
      // keep-alive 來不及處理就被判定 server timeout，於是「連上→逾時→
      // 重連」變成一個不斷打 API 的忙碌迴圈。背景時本來也不需要即時連線，
      // 回前景的 visibilitychange 會把它接回來。
      if (document.visibilityState !== 'visible') return

      const delay = RETRY_DELAYS[Math.min(attempt, RETRY_DELAYS.length - 1)]
      attempt += 1
      if (retryTimer) clearTimeout(retryTimer)
      retryTimer = setTimeout(start, delay)
    }

    const start = () => {
      if (stopped) return
      if (connection.state !== signalR.HubConnectionState.Disconnected) return
      // 排程時畫面還在前景，引爆時可能已經轉背景了。這裡再擋一次，
      // 不然「背景不重連」的設計會被已排程的計時器繞過去。
      if (document.visibilityState !== 'visible') return

      connection.start()
        .then(() => {
          if (stopped) return
          connectedAt = Date.now()
          replay()
          refreshUnread()
        })
        .catch(scheduleRetry)
    }

    connection.onreconnected(() => {
      // 重連拿到新的 connectionId，個人群組由伺服器的 OnConnectedAsync 自動補，
      // 但房間群組的成員資格沒了，要自己重放；斷線期間的訊息也要重新校正。
      replay()
      refreshUnread()
    })

    connection.onclose(() => {
      if (stopped) return
      // 自動重連用盡才會走到這裡。連線現在跨分頁存活，沒有重新掛載可以靠，
      // 所以自己退避重啟——否則鎖屏一分鐘回來就永久收不到訊息。
      //
      // 只有「活夠久」才把退避歸零。連上就歸零的話，一旦遇到連上隨即斷線
      // 的情況（背景節流、網路不穩），退避永遠從 0 開始，等於全速重連。
      if (connectedAt > 0 && Date.now() - connectedAt > STABLE_AFTER_MS) attempt = 0
      connectedAt = 0
      scheduleRetry()
    })

    start()

    const onVisible = () => {
      if (document.visibilityState !== 'visible') return
      // 回到前景：背景期間放棄的重連在這裡接回來，退避也重新計算
      if (connection.state === signalR.HubConnectionState.Disconnected) {
        attempt = 0
        start()
      }
      // 背景期間漏掉的訊息要補上，這次不受節流限制
      refreshUnread({ force: true })

      // 人回到前景時還停在某間房 → 那些訊息他現在就看得到，水位要跟上。
      // 少了這一步，鎖屏期間收到的訊息會讓他正在看的那間房一直亮著紅點，
      // 而且 activeRoomId 沒變、effect 不會重跑，退出再進去才清得掉。
      const active = activeRoomRef.current
      if (active) markRead(active)
    }
    document.addEventListener('visibilitychange', onVisible)

    return () => {
      stopped = true
      if (retryTimer) clearTimeout(retryTimer)
      document.removeEventListener('visibilitychange', onVisible)
      if (refetchTimerRef.current) clearTimeout(refetchTimerRef.current)
      markTimers.forEach((timer) => clearTimeout(timer))
      markTimers.clear()
      connectionRef.current = null
      connection.stop().catch(() => {})
    }
  }, [userId, refreshUnread, scheduleMarkRead, markRead])

  // 登出後不要繼續顯示上一個人的紅點。用衍生值而不是在登出時清 state，
  // 省掉一次會連鎖重繪的同步 setState。
  // owner 對不上（剛換帳號、還沒校正回來）就當成空的，
  // 不要讓 B 看到 A 的紅點數字而列表裡找不到來源。
  const visibleUnread = userId && unread.owner === userId ? unread.byRoom : EMPTY_UNREAD

  const totalUnread = useMemo(
    () => Object.values(visibleUnread).reduce((sum, n) => sum + n, 0),
    [visibleUnread],
  )

  const value = useMemo<ChatRealtimeValue>(() => ({
    unreadByRoom: visibleUnread,
    totalUnread,
    joinRooms,
    setActiveRoom,
    markRead,
    forgetRoom,
    refreshUnread,
    subscribe,
  }), [visibleUnread, totalUnread, joinRooms, setActiveRoom, markRead, forgetRoom, refreshUnread, subscribe])

  return <ChatRealtimeContext.Provider value={value}>{children}</ChatRealtimeContext.Provider>
}

export function useChatRealtime(): ChatRealtimeValue {
  const value = useContext(ChatRealtimeContext)
  if (!value) throw new Error('useChatRealtime 必須在 ChatRealtimeProvider 之內使用')
  return value
}
