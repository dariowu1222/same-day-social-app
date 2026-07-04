import { request } from '../../shared/api/httpClient'
import type { MatchResult } from './types'

export async function getTodayMatches(userId: string) {
  return request<MatchResult[]>(`/api/matches/today/${userId}`)
}

export async function likeMatch(matchId: string) {
  return request<unknown>(`/api/matches/${matchId}/like`, { method: 'POST' })
}
