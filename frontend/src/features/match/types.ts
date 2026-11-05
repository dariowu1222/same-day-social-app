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

/** 右滑的結果。isMutual 為 true 代表雙方都按了同頻，聊天室已自動開好。 */
export type LikeResult = {
  match: {
    id: string
    userId: string
    matchedUserId: string
    userLiked: boolean
    matchedUserLiked: boolean
  }
  isMutual: boolean
  chatRoomId?: string | null
  /**
   * 配對成立了，但聊天對象額滿所以沒開成聊天室。
   * 與一般的開房失敗不同：這個重試幾次都一樣，要先去結束幾段對話。
   */
  partnerLimitReached?: boolean
}

/** 同時進行中的聊天對象額度。used 只算還沒到期的房間，與開房時的判斷同一條規則。 */
export type PartnerQuota = {
  used: number
  limit: number
  full: boolean
}
