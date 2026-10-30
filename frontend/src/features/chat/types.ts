export type ChatRoom = {
  id: string
  userIds: string[]
  sourceType: string
  sourceId: string
  createdAt: string
}

export type ChatMemberSetting = {
  chatRoomId: string
  noteName?: string | null
  pinned: boolean
  muted: boolean
}

export type ChatMessage = {
  id: string
  chatRoomId: string
  senderId: string
  content: string
  createdAt: string
  // 引用回覆 / 收回（後端 Task #4 填入；前端可選讀）
  quotedMessageId?: string | null
  quotedSenderName?: string | null
  quotedContent?: string | null
  isRecalled?: boolean
  // 僅存在於本地：樂觀更新的送出狀態。送達後由伺服器版本取代，此欄位即消失。
  status?: 'sending' | 'failed'
}

/**
 * 聊天列表一次拿完所需資料（GET /api/chats/user/{id}/overview）。
 * 先前列表是每間房各打一次 /messages、每個對方各打一次 /profile，
 * 30 間房就是 60 幾個請求全擠在瀏覽器的 6 條連線上。
 */
export type ChatRoomOverview = {
  room: ChatRoom
  peerUserId?: string | null
  peerNickname?: string | null
  peerPhotoUrl?: string | null
  lastMessage?: ChatMessage | null
}

/** 列表用的精簡對方資料。完整 profile 只在進房時才抓（peek sheet 需要）。 */
export type ChatPeer = { nickname?: string | null; photoUrl?: string | null }
