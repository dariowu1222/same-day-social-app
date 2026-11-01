import { request } from '../../shared/api/httpClient'
import type { MatchResult, LikeResult } from './types'

export async function getTodayMatches(userId: string) {
  return request<MatchResult[]>(`/api/matches/today/${userId}`)
}

export async function likeMatch(matchId: string) {
  return request<LikeResult>(`/api/matches/${matchId}/like`, { method: 'POST' })
}

/** 左滑（略過）。後端做成冪等，重複呼叫安全。 */
export async function passMatch(matchId: string) {
  return request<boolean>(`/api/matches/${matchId}/pass`, { method: 'POST' })
}
