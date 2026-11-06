import { apiPath, request } from '../../shared/api/httpClient'
import type { MatchResult, LikeResult, PartnerQuota } from './types'

export async function getTodayMatches(userId: string) {
  return request<MatchResult[]>(apiPath`/api/matches/today/${userId}`)
}

export async function likeMatch(matchId: string) {
  return request<LikeResult>(apiPath`/api/matches/${matchId}/like`, { method: 'POST' })
}

/**
 * 聊天對象額度。放在這裡而不是 chat 的 api：用到它的只有滑卡頁，
 * 而且要在開始滑之前就問，不是進聊天室才問。
 */
export async function getPartnerQuota() {
  return request<PartnerQuota>('/api/chats/partner-quota')
}

/** 左滑（略過）。後端做成冪等，重複呼叫安全。 */
export async function passMatch(matchId: string) {
  return request<boolean>(apiPath`/api/matches/${matchId}/pass`, { method: 'POST' })
}
