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
}
