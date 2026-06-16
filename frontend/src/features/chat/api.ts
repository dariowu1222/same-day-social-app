import { request } from '../../shared/api/httpClient'
import type { ChatRoom, ChatMessage } from './types'

export async function getChatRooms(userId: string) {
  return request<ChatRoom[]>(`/api/chats/user/${userId}`)
}

export async function createChatRoom(payload: { userIds: string[]; sourceType: string; sourceId: string }) {
  return request<ChatRoom>('/api/chats', { method: 'POST', body: JSON.stringify(payload) })
}

export async function getMessages(chatRoomId: string) {
  return request<ChatMessage[]>(`/api/chats/${chatRoomId}/messages`)
}

export type QuoteInfo = { quotedMessageId: string; quotedSenderName: string; quotedContent: string }

export async function sendMessage(chatRoomId: string, content: string, quote?: QuoteInfo) {
  return request<ChatMessage>(`/api/chats/${chatRoomId}/messages`, { method: 'POST', body: JSON.stringify({ content, ...quote }) })
}

export async function recallMessage(chatRoomId: string, messageId: string) {
  return request<ChatMessage>(`/api/chats/${chatRoomId}/messages/${messageId}/recall`, { method: 'POST' })
}
