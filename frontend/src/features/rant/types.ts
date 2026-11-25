export type RantReply = {
  id: string
  /** 這則是不是我回的。未登入時一律 false。 */
  isMine: boolean
  /** 我按過這則的愛心了沒。舊版後端不會送，當成沒按過。 */
  isLiked?: boolean
  /**
   * 這則被我（貼文作者）隱藏了——自己被藏，或是它回的那一則被藏。
   * 只有貼文作者會拿到 true：其他人根本拿不到這一則，而寫這一則的人自己
   * 拿到的是 false（不告訴他被藏了）。
   */
  isHidden?: boolean
  /** 可以取消隱藏：是我親手藏的這一則，不是跟著上一則一起被藏的。 */
  canUnhide?: boolean
  nickname: string
  content: string
  imageUrl?: string | null
  thumbUrl?: string | null
  audioUrl?: string | null
  likeCount: number
  parentReplyId?: string | null
  /** 後端一直都有送，前端以前沒拿來用。 */
  createdAt?: string
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
  /** 我按過這篇的愛心了沒。舊版後端不會送，當成沒按過。 */
  isLiked?: boolean
  /**
   * 作者已經刪掉這篇，剩下的是底下別人的回應。
   * true 的時候內容、圖片、標籤、作者名字全部是空的，isMine 一定是 false。
   * 牆上不會出現這種貼文，只有帶著連結進詳情頁才會遇到。
   */
  isDeleted?: boolean
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
  /**
   * 前三個回應的人的名字，收合的卡片疊頭像用。
   * 只有列表（摘要）會帶；那時候 replies 是空的，要看回應得另外抓這一篇。
   * 單篇與互動端點回來的沒有這一欄，回應是完整的。
   */
  replyNames?: string[] | null
  replies: RantReply[]
}

// 巢狀回覆攤平成單層陣列（深度優先）。
export function flattenReplies(replies: RantReply[]): RantReply[] {
  return replies.flatMap((r) => [r, ...flattenReplies(r.replies ?? [])])
}

/**
 * 把某一則回應標成「被我藏起來了」（或放出來），回傳新的貼文物件。
 *
 * 給貼文作者按下「隱藏／取消隱藏」的那一刻用：畫面先照這個結果變，不等後端。
 * 實測後端那一趟要 0.7–1.6 秒，等它回來才動的話，按下去像是沒按到。
 * 後端回來之後會整篇換成它給的那一份，所以這裡算錯也只會錯那一秒。
 *
 * 規則跟後端（RantService.Assemble）一樣：一則回應被藏，接在它底下的也跟著藏；
 * canUnhide 只給親手藏的那一則。
 */
export function withReplyHidden(post: RantPost, replyId: string, hidden: boolean): RantPost {
  return {
    ...post,
    replies: post.replies.map((top) => {
      const subs = top.replies ?? []
      if (top.id !== replyId && !subs.some((sub) => sub.id === replyId)) return top

      const all = [top, ...subs]
      const byId = new Map(all.map((reply) => [reply.id, reply]))
      // 親手藏的：這一次按的那一則照新的值，其餘照原本的
      const direct = new Map(all.map((reply) => [reply.id, reply.id === replyId ? hidden : !!reply.canUnhide]))
      // 自己被藏，或往上任何一層被藏。depth 是保險：資料成環時不會無限往上
      const covered = (reply: RantReply, depth = 0): boolean => {
        if (direct.get(reply.id)) return true
        const parent = reply.parentReplyId ? byId.get(reply.parentReplyId) : undefined
        return parent !== undefined && depth < 50 && covered(parent, depth + 1)
      }
      const mark = (reply: RantReply): RantReply => ({
        ...reply,
        isHidden: covered(reply),
        canUnhide: !!direct.get(reply.id),
      })
      return { ...mark(top), replies: subs.map(mark) }
    }),
  }
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
