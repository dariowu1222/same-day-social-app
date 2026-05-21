const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'

type ApiResponse<T> = {
  success: boolean
  code?: string
  message?: string
  warning?: string
  data: T
}

async function request<T>(path: string, options?: RequestInit): Promise<ApiResponse<T>> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: { 'Content-Type': 'application/json', ...(options?.headers ?? {}) },
    ...options,
  })
  const body = (await response.json()) as ApiResponse<T>
  if (!response.ok || !body.success) {
    throw new Error(body.message ?? 'API request failed')
  }
  return body
}

export async function demoLogin(nickname: string) {
  const response = await request<{ userId: string; nickname: string }>('/api/auth/demo-login', {
    method: 'POST',
    body: JSON.stringify({ nickname }),
  })
  return response.data
}

export async function createTodayEntry(payload: {
  userId: string
  content: string
  responseMode: string
  visibility: string
}) {
  return request<{ entry: TodayEntry; analysis: TodayAnalysis }>('/api/today-entries', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export async function getTodayMatches(userId: string) {
  return request<MatchResult[]>(`/api/matches/today/${userId}`)
}

export async function getRants() {
  return request<RantPost[]>('/api/rants')
}

export async function createRant(payload: { userId: string; nickname: string; content: string; mode: string }) {
  return request<RantPost>('/api/rants', { method: 'POST', body: JSON.stringify(payload) })
}

export async function understandRant(rantId: string) {
  return request<RantPost>(`/api/rants/${rantId}/understand`, { method: 'POST' })
}

export async function replyRant(rantId: string, payload: { userId: string; nickname: string; content: string }) {
  return request<RantPost>(`/api/rants/${rantId}/replies`, { method: 'POST', body: JSON.stringify(payload) })
}

export async function reportRant(rantId: string) {
  return request<RantPost>(`/api/rants/${rantId}/report`, { method: 'POST' })
}

export async function getTasks() {
  return request<SocialTask[]>('/api/tasks')
}

export async function joinTask(taskId: string, userId: string) {
  return request<SocialTask>(`/api/tasks/${taskId}/join`, { method: 'POST', body: JSON.stringify({ userId }) })
}

export async function getChatRooms(userId: string) {
  return request<ChatRoom[]>(`/api/chats/user/${userId}`)
}

export async function createChatRoom(payload: { userIds: string[]; sourceType: string; sourceId: string }) {
  return request<ChatRoom>('/api/chats', { method: 'POST', body: JSON.stringify(payload) })
}

export async function getMessages(chatRoomId: string) {
  return request<ChatMessage[]>(`/api/chats/${chatRoomId}/messages`)
}

export async function sendMessage(chatRoomId: string, payload: { senderId: string; content: string }) {
  return request<ChatMessage>(`/api/chats/${chatRoomId}/messages`, { method: 'POST', body: JSON.stringify(payload) })
}

export type TodayAnalysis = {
  eventType: string
  emotionTags: string[]
  valueTags: string[]
  interestTags: string[]
  responseMode: string
}

export type TodayEntry = TodayAnalysis & {
  id: string
  userId: string
  content: string
  visibility: string
  createdAt: string
}

export type MatchResult = {
  matchId: string
  matchedUserId: string
  nickname: string
  matchScore: number
  matchType: string
  sharedTags: string[]
  reason: string
  icebreaker: string
  todaySummary: string
}

export type RantPost = {
  id: string
  userId: string
  nickname: string
  content: string
  mode: string
  emotionTags: string[]
  createdAt: string
  likeCount: number
  replyCount: number
  reportCount: number
  replies: { id: string; nickname: string; content: string }[]
}

export type SocialTask = {
  id: string
  title: string
  description: string
  category: string
  duration: string
  difficulty: string
  participantLimit: number
  participantUserIds: string[]
}

export type ChatRoom = {
  id: string
  userIds: string[]
  sourceType: string
  sourceId: string
  createdAt: string
}

export type ChatMessage = {
  id: string
  chatRoomId: string
  senderId: string
  content: string
  createdAt: string
}
