import { request } from '../../shared/api/httpClient'
import type { AppNotification } from './types'

export const NOTIFICATION_PAGE_SIZE = 20

export async function getNotifications(cursor?: string, limit = NOTIFICATION_PAGE_SIZE) {
  const params = new URLSearchParams({ limit: String(limit) })
  if (cursor) params.set('cursor', cursor)
  return request<AppNotification[]>(`/api/notifications?${params.toString()}`)
}

export async function getUnreadCount() {
  return request<number>('/api/notifications/unread-count')
}

export async function markAllNotificationsRead() {
  return request<number>('/api/notifications/read', { method: 'PUT' })
}
