import { request } from '../../shared/api/httpClient'
import type { SocialTask } from './types'

export async function getTasks() {
  return request<SocialTask[]>('/api/tasks')
}

export async function joinTask(taskId: string, userId: string) {
  return request<SocialTask>(`/api/tasks/${taskId}/join`, { method: 'POST', body: JSON.stringify({ userId }) })
}
