export type MatchResult = {
  matchId: string
  matchedUserId: string
  nickname: string
  matchScore: number
  matchType: string
  sharedTags: string[]
  reason: string
  icebreaker: string
  todaySummary: string
  // 後端豐富化欄位（可能為空，前端做優雅退場）
  age?: number | null
  city?: string | null
  bio?: string | null
  interests?: string[] | null
  photos?: string[] | null
  birthday?: string | null
  personalityTags?: string[] | null
  appearanceTags?: string[] | null
  relationship?: string | null
}
