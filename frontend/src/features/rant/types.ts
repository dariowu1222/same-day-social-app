export type RantReply = {
  id: string
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
  userId: string
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
  reportCount: number
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
