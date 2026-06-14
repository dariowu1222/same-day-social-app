import { request } from '../../shared/api/httpClient'
import type { RantPost } from './types'

export async function getRants() {
  return request<RantPost[]>('/api/rants')
}

export async function createRant(payload: { nickname: string; content: string; mode: string; hashtags?: string[]; imageDataUrl?: string | null; audioDataUrl?: string | null }) {
  return request<RantPost>('/api/rants', { method: 'POST', body: JSON.stringify(payload) })
}

export async function understandRant(rantId: string) {
  return request<RantPost>(`/api/rants/${rantId}/understand`, { method: 'POST' })
}

export async function replyRant(rantId: string, payload: { nickname: string; content: string; imageDataUrl?: string | null; audioDataUrl?: string | null; parentReplyId?: string | null }) {
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
