export type RantReply = {
  id: string
  /** 這則是不是我回的。未登入時一律 false。 */
  isMine: boolean
  nickname: string
  content: string
  imageUrl?: string | null
  thumbUrl?: string | null
  audioUrl?: string | null
  likeCount: number
  parentReplyId?: string | null
  replies?: RantReply[]
}

export type RantPost = {
  id: string
  /**
   * 這篇是不是我發的。
   *
   * 以前這裡是 userId。那兩支 GET 沒有登入限制，任何人都讀得到，帶著作者的
   * user id 等於匿名發文可以被對應回某一個帳號，再拿去 /api/profile/{userId}
   * 查出這個人是誰。前端要的其實只有「要不要顯示刪除選單」。
   */
  isMine: boolean
  nickname: string
  content: string
  mode: string
  emotionTags: string[]
  hashtags: string[]
  imageUrls?: string[]
  thumbUrls?: string[]
  imageUrl?: string | null
  audioUrl?: string | null
  createdAt: string
  likeCount: number
  replyCount: number
  replies: RantReply[]
}

// 巢狀回覆攤平成單層陣列（深度優先）。
export function flattenReplies(replies: RantReply[]): RantReply[] {
  return replies.flatMap((r) => [r, ...flattenReplies(r.replies ?? [])])
}

// 一張貼文圖片：full 供點開檢視、thumb 供列表顯示（省頻寬）。
export type PostImage = { full: string; thumb: string }

// 取貼文圖片清單：優先多圖 imageUrls，舊資料回退單圖 imageUrl。
// 沒有縮圖的舊資料，thumb 回退成原圖，確保不會破圖。
export function postImages(post: RantPost): PostImage[] {
  const fulls =
    post.imageUrls && post.imageUrls.length > 0
      ? post.imageUrls
      : post.imageUrl
        ? [post.imageUrl]
        : []
  return fulls.map((full, i) => ({ full, thumb: post.thumbUrls?.[i] || full }))
}
