import { request } from '../../shared/api/httpClient'
import type { UserProfile } from './types'

export async function getProfile(userId: string) {
  return request<UserProfile>(`/api/profile/${userId}`)
}

export async function updateProfile(userId: string, payload: {
  nickname?: string
  bio?: string
  interestTags?: string[]
  valueTags?: string[]
  photoDataUrls?: string[]
  birthday?: string
}) {
  return request<UserProfile>(`/api/profile/${userId}`, {
    method: 'PUT',
    body: JSON.stringify(payload),
  })
}
