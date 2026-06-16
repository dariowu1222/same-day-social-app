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
}
