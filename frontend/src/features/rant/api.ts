import { apiPath, request } from '../../shared/api/httpClient'
import type { RantPost } from './types'

export const RANT_PAGE_SIZE = 20

// 列表只拿摘要：每篇有幾則回應、前三個回應的人叫什麼（replyCount、replyNames），
// replies 是空的。回應本身等點開那一篇才用 getRant 抓。
// 以前每一頁都把每一篇的全部回應一起帶回來，而絕大多數根本沒有人點開。
export async function getRants(cursor?: string, limit = RANT_PAGE_SIZE) {
  const params = new URLSearchParams({ limit: String(limit), replies: 'summary' })
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

// 收回按在貼文上的愛心。跟 understandRant 成對：一支按、一支收，兩支都可以重複呼叫。
export async function unlikeRant(rantId: string) {
  return request<RantPost>(apiPath`/api/rants/${rantId}/understand`, { method: 'DELETE' })
}

export async function replyRant(rantId: string, payload: { content: string; imageUrl?: string | null; thumbUrl?: string | null; audioUrl?: string | null; parentReplyId?: string | null }) {
  return request<RantPost>(apiPath`/api/rants/${rantId}/replies`, { method: 'POST', body: JSON.stringify(payload) })
}

export async function likeReply(rantId: string, replyId: string) {
  return request<RantPost>(apiPath`/api/rants/${rantId}/replies/${replyId}/understand`, { method: 'POST' })
}

export async function unlikeReply(rantId: string, replyId: string) {
  return request<RantPost>(apiPath`/api/rants/${rantId}/replies/${replyId}/understand`, { method: 'DELETE' })
}

// 收回自己的一則回應。只有底下還沒有人接話的才收得回來，否則後端回
// REPLY_HAS_CHILDREN（訊息已經是給人看的中文）。回傳更新後的整篇。
//
// 有一種情況 data 會是 null：貼文已經被作者刪掉，而收回的是底下最後一則回應，
// 整串沒有東西可看了。那只會發生在詳情頁（牆上沒有已刪除的貼文），詳情頁是
// 收回之後重抓，不看這裡的 data。
export async function deleteReply(rantId: string, replyId: string) {
  return request<RantPost>(apiPath`/api/rants/${rantId}/replies/${replyId}`, { method: 'DELETE' })
}

// 貼文作者隱藏自己貼文底下的一則回應：其他人看不到，作者自己看到的是收合的樣子，
// 回應的人自己還看得到、也不會被通知。回傳更新後的整篇。
export async function hideReply(rantId: string, replyId: string) {
  return request<RantPost>(apiPath`/api/rants/${rantId}/replies/${replyId}/hide`, { method: 'POST' })
}

export async function unhideReply(rantId: string, replyId: string) {
  return request<RantPost>(apiPath`/api/rants/${rantId}/replies/${replyId}/hide`, { method: 'DELETE' })
}

export async function reportRant(rantId: string) {
  return request<RantPost>(apiPath`/api/rants/${rantId}/report`, { method: 'POST' })
}

export async function deleteRant(rantId: string) {
  return request<boolean>(apiPath`/api/rants/${rantId}`, { method: 'DELETE' })
}
