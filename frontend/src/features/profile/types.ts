export type UserProfile = {
  id: string
  nickname: string
  bio: string
  interestTags: string[]
  valueTags: string[]
  photoDataUrls: string[]
  // 自己的生日。看別人的檔案時後端不給這一欄，改給下面的 age 與 zodiac。
  birthday?: string
  // 看別人的檔案時才有：年齡與星座由後端從生日算好，完整的出生年月日不離開伺服器。
  age?: number | null
  zodiac?: string | null
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
  preferredArea?: string | null
  // 自己的所在縣市（會顯示在配對卡上，也是距離偏好的起點）
  locationArea?: string | null
  languages?: string[]
  activeTime?: string
  voiceFirst?: boolean
  meetSoon?: boolean
}
