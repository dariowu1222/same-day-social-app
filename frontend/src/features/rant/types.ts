export type RantReply = {
  id: string
  nickname: string
  content: string
  imageDataUrl?: string | null
  audioDataUrl?: string | null
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
  imageDataUrls?: string[]
  imageDataUrl?: string | null
  audioDataUrl?: string | null
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

// 取貼文圖片清單：優先多圖 imageDataUrls，舊資料回退單圖 imageDataUrl。
export function postImages(post: RantPost): string[] {
  if (post.imageDataUrls && post.imageDataUrls.length > 0) return post.imageDataUrls
  return post.imageDataUrl ? [post.imageDataUrl] : []
}
