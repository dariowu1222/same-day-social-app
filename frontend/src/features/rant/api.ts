import { apiPath, request } from '../../shared/api/httpClient'
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
  return request<RantPost>(apiPath`/api/rants/${rantId}`)
}

// payload 裡原本有 nickname，由這邊從 AuthContext 帶上去。拿掉了：後端現在自己
// 去 users 查，因為那份 AuthContext 的使用者是登入當下寫進 localStorage 的副本——
// 在沒按過「儲存」的另一台裝置上，它會停在改名前的舊名字，連新發的文都會被蓋上舊名。
export async function createRant(payload: { content: string; mode: string; hashtags?: string[]; imageUrls?: string[]; thumbUrls?: string[]; audioUrl?: string | null }) {
  return request<RantPost>('/api/rants', { method: 'POST', body: JSON.stringify(payload) })
}

export async function understandRant(rantId: string) {
  return request<RantPost>(apiPath`/api/rants/${rantId}/understand`, { method: 'POST' })
}

export async function replyRant(rantId: string, payload: { content: string; imageUrl?: string | null; thumbUrl?: string | null; audioUrl?: string | null; parentReplyId?: string | null }) {
  return request<RantPost>(apiPath`/api/rants/${rantId}/replies`, { method: 'POST', body: JSON.stringify(payload) })
}

export async function likeReply(rantId: string, replyId: string) {
  return request<RantPost>(apiPath`/api/rants/${rantId}/replies/${replyId}/understand`, { method: 'POST' })
}

export async function reportRant(rantId: string) {
  return request<RantPost>(apiPath`/api/rants/${rantId}/report`, { method: 'POST' })
}

export async function deleteRant(rantId: string) {
  return request<boolean>(apiPath`/api/rants/${rantId}`, { method: 'DELETE' })
}
