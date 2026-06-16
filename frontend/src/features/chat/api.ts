import { request } from '../../shared/api/httpClient'
import type { ChatRoom, ChatMessage, ChatMemberSetting } from './types'

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

export async function getMemberSettings(userId: string) {
  return request<ChatMemberSetting[]>(`/api/chats/user/${userId}/settings`)
}

export async function updateChatSetting(chatRoomId: string, patch: { noteName?: string | null; pinned?: boolean; muted?: boolean }) {
  return request<ChatMemberSetting>(`/api/chats/${chatRoomId}/settings`, { method: 'PUT', body: JSON.stringify(patch) })
}

export async function leaveChatRoom(chatRoomId: string) {
  return request<boolean>(`/api/chats/${chatRoomId}/leave`, { method: 'POST' })
}

export async function blockChatUser(chatRoomId: string) {
  return request<boolean>(`/api/chats/${chatRoomId}/block`, { method: 'POST' })
}

export async function reportChatUser(chatRoomId: string, reason?: string) {
  return request<boolean>(`/api/chats/${chatRoomId}/report`, { method: 'POST', body: JSON.stringify({ reason }) })
}
