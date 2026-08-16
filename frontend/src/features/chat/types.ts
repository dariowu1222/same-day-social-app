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
