import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Plus, Search } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { deleteRant, getRants, replyRant, reportRant, understandRant, RANT_PAGE_SIZE } from './api'
import type { RantPost } from './types'
import { EMPTY_MEDIA, type MediaState } from '../../shared/ui/MediaInput'
import RantPostCard from './RantPostCard'

export default function RantBoardPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [posts, setPosts] = useState<RantPost[]>([])
  const [hasMore, setHasMore] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const [replies, setReplies] = useState<Record<string, string>>({})
  const [replyMedia, setReplyMedia] = useState<Record<string, MediaState>>({})

  // 互動後重新載入第一頁（回到最新）
  const loadPosts = useCallback(async () => {
    const res = await getRants()
    setPosts(res.data)
    setHasMore(res.data.length === RANT_PAGE_SIZE)
  }, [])

  useEffect(() => { void loadPosts() }, [loadPosts])

  async function loadMore() {
    if (loadingMore || !hasMore || posts.length === 0) return
    setLoadingMore(true)
    try {
      const cursor = posts[posts.length - 1].createdAt
      const res = await getRants(cursor)
      setPosts((prev) => [...prev, ...res.data])
      setHasMore(res.data.length === RANT_PAGE_SIZE)
    } finally {
      setLoadingMore(false)
    }
  }

  async function updatePost(action: () => Promise<unknown>) {
    await action()
    await loadPosts()
  }

  if (!user) return null

  return (
    <div className="page rant-page">

      {/* 13.1 搜尋入口（sticky）：只作為入口，點擊進入獨立搜尋頁 */}
      <div className="rant-search-entry-bar">
        <button className="rant-search-entry" type="button" onClick={() => navigate('/rant/search')}>
          <Search size={16} className="rant-search-entry-icon" />
          <span className="rant-search-entry-placeholder">搜尋樹洞的心事</span>
        </button>
      </div>

      <p className="rant-board-title">這裡可以說說今天不太想放在心裡的事</p>

      <section className="list">
        {posts.map((post) => (
          <RantPostCard
            key={post.id}
            post={post}
            replyText={replies[post.id] ?? ''}
            onReplyTextChange={(value) => setReplies({ ...replies, [post.id]: value })}
            onUnderstand={() => updatePost(() => understandRant(post.id))}
            onReport={() => updatePost(() => reportRant(post.id))}
            replyMedia={replyMedia[post.id] ?? EMPTY_MEDIA}
            onReplyMediaChange={(media) => setReplyMedia({ ...replyMedia, [post.id]: media })}
            onReply={(parentReplyId) =>
              updatePost(() => replyRant(post.id, { nickname: user.nickname, content: replies[post.id] ?? '', ...(replyMedia[post.id] ?? EMPTY_MEDIA), parentReplyId }))
            }
            onDelete={() => updatePost(() => deleteRant(post.id))}
            onLikedReply={loadPosts}
            onHashtagClick={(tag) => navigate('/rant/search', { state: { q: tag } })}
            currentUserId={user.userId}
          />
        ))}

        {hasMore && posts.length > 0 && (
          <button className="rant-load-more" type="button" onClick={loadMore} disabled={loadingMore}>
            {loadingMore ? '載入中⋯' : '載入更多'}
          </button>
        )}
      </section>

      {/* FAB：右下角發文按鈕 */}
      <button
        className="rant-fab"
        onClick={() => navigate('/rant/new')}
        aria-label="說說看"
      >
        <Plus size={24} />
      </button>
    </div>
  )
}
