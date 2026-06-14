import { request } from '../../shared/api/httpClient'
import type { TodayAnalysis, TodayEntry } from './types'

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
