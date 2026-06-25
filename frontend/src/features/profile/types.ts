export type UserProfile = {
  id: string
  nickname: string
  bio: string
  interestTags: string[]
  valueTags: string[]
  photoDataUrls: string[]
  birthday?: string
  responsePreference?: string
  // 個人資料欄位（性別必填，其餘可空）
  gender?: string
  relationship?: string
  personalityTags?: string[]
  appearanceTags?: string[]
  height?: number | null
  weight?: number | null
  occupation?: string
  school?: string
  bloodType?: string
  // 交友意圖
  datingGoal?: string
  lookingFor?: string
  ageMin?: number | null
  ageMax?: number | null
  distanceKm?: number | null
  languages?: string[]
  activeTime?: string
  voiceFirst?: boolean
  meetSoon?: boolean
}
