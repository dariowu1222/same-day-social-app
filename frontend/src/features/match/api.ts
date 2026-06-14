import { request } from '../../shared/api/httpClient'
import type { MatchResult } from './types'

export async function getTodayMatches(userId: string) {
  return request<MatchResult[]>(`/api/matches/today/${userId}`)
}
