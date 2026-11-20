import { apiPath, request } from '../../shared/api/httpClient'
import type { UserProfile } from './types'

export async function getProfile(userId: string) {
  return request<UserProfile>(apiPath`/api/profile/${userId}`)
}

export async function updateProfile(userId: string, payload: {
  nickname?: string
  bio?: string
  interestTags?: string[]
  valueTags?: string[]
  photoDataUrls?: string[]
  birthday?: string
  gender?: string
  relationship?: string
  personalityTags?: string[]
  appearanceTags?: string[]
  height?: number | null
  weight?: number | null
  occupation?: string
  school?: string
  bloodType?: string
  datingGoal?: string
  lookingFor?: string
  ageMin?: number | null
  ageMax?: number | null
  // 地區三欄。空字串／0 是「清掉」，沒送（undefined）是「這次不改」。
  // 距離與偏好縣市擇一，後端寫入其中一個就會清掉另一個。
  locationArea?: string
  distanceKm?: number | null
  preferredArea?: string | null
  languages?: string[]
  activeTime?: string
  voiceFirst?: boolean
  meetSoon?: boolean
}) {
  return request<UserProfile>(apiPath`/api/profile/${userId}`, {
    method: 'PUT',
    body: JSON.stringify(payload),
  })
}
