import { request } from '../../shared/api/httpClient'
import type { RantPost } from './types'

export const RANT_PAGE_SIZE = 20

export async function getRants(cursor?: string, limit = RANT_PAGE_SIZE) {
  const params = new URLSearchParams({ limit: String(limit) })
  if (cursor) params.set('cursor', cursor)
  return request<RantPost[]>(`/api/rants?${params.toString()}`)
}

// 單篇。詳情頁先前是抓整個列表再 .find()，使用者按過「載入更多」之後
// 點第 21 篇以後的貼文永遠找不到，畫面會卡在「載入中…」。
export async function getRant(rantId: string) {
  return request<RantPost>(`/api/rants/${rantId}`)
}

export async function createRant(payload: { nickname: string; content: string; mode: string; hashtags?: string[]; imageUrls?: string[]; thumbUrls?: string[]; audioUrl?: string | null }) {
  return request<RantPost>('/api/rants', { method: 'POST', body: JSON.stringify(payload) })
}

export async function understandRant(rantId: string) {
  return request<RantPost>(`/api/rants/${rantId}/understand`, { method: 'POST' })
}

export async function replyRant(rantId: string, payload: { nickname: string; content: string; imageUrl?: string | null; thumbUrl?: string | null; audioUrl?: string | null; parentReplyId?: string | null }) {
  return request<RantPost>(`/api/rants/${rantId}/replies`, { method: 'POST', body: JSON.stringify(payload) })
}

export async function likeReply(rantId: string, replyId: string) {
  return request<RantPost>(`/api/rants/${rantId}/replies/${replyId}/understand`, { method: 'POST' })
}

export async function reportRant(rantId: string) {
  return request<RantPost>(`/api/rants/${rantId}/report`, { method: 'POST' })
}

export async function deleteRant(rantId: string) {
  return request<boolean>(`/api/rants/${rantId}`, { method: 'DELETE' })
}
