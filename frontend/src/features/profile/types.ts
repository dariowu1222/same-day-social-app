export type UserProfile = {
  id: string
  nickname: string
  bio: string
  interestTags: string[]
  valueTags: string[]
  photoDataUrls: string[]
  birthday?: string
  responsePreference?: string
}
