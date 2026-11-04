import { request } from '../../shared/api/httpClient'
import type { TodayAnalysis, TodayEntry } from './types'

export async function createTodayEntry(payload: {
  userId: string
  content: string
  responseMode: string
  visibility: string
  /** 今日頁標題旁的「會員測試AI」開關。後端沒設 Ai:ApiKey 時這個值不會有作用。 */
  useAi: boolean
}) {
  return request<{ entry: TodayEntry; analysis: TodayAnalysis }>('/api/today-entries', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}
