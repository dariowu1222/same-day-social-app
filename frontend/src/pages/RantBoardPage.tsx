import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Plus, X } from 'lucide-react'
import type { DemoUser } from '../App'
import { deleteRant, getRants, replyRant, reportRant, understandRant, type RantPost } from '../api/client'
import { EMPTY_MEDIA, type MediaState } from '../components/MediaInput'
import RantPostCard from '../components/RantPostCard'
import { Search } from 'lucide-react'

type Props = {
  user: DemoUser
}

export default function RantBoardPage({ user }: Props) {
  const navigate = useNavigate()
  const [posts, setPosts] = useState<RantPost[]>([])
  const [replies, setReplies] = useState<Record<string, string>>({})
  const [replyMedia, setReplyMedia] = useState<Record<string, MediaState>>({})
  const [searchQuery, setSearchQuery] = useState('')
  const [activeQuery, setActiveQuery] = useState('')

  const filteredPosts = activeQuery.trim()
    ? posts.filter((post) => {
        const q = activeQuery.trim().toLowerCase()
        return (
          post.content.toLowerCase().includes(q) ||
          post.hashtags?.some((tag) => tag.toLowerCase().includes(q))
        )
      })
    : posts

  function commitSearch() {
    setActiveQuery(searchQuery)
  }

  function clearSearch() {
    setSearchQuery('')
    setActiveQuery('')
  }

  useEffect(() => {
    loadPosts()
  }, [])

  async function loadPosts() {
    const response = await getRants()
    setPosts(response.data)
  }

  async function updatePost(action: () => Promise<unknown>) {
    await action()
    await loadPosts()
  }

  return (
    <div className="page">
      {/* 搜尋欄（sticky）*/}
      <div className="rant-search-bar">
        <div className="rant-search-inner">
          <Search size={15} className="rant-search-icon" />
          <input
            className="rant-search-input"
            placeholder="搜尋…"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && commitSearch()}
          />
          {searchQuery && (
            <button className="rant-search-clear" onClick={clearSearch}>
              <X size={14} />
            </button>
          )}
        </div>
        {activeQuery.trim() && (
          <span className="rant-search-count">
            {filteredPosts.length > 0 ? `${filteredPosts.length} 則` : '無符合貼文'}
          </span>
        )}
      </div>

      <p className="rant-board-title">這裡可以說說今天不太想放在心裡的事</p>

      <section className="list">
        {filteredPosts.length === 0 && activeQuery.trim() ? (
          <p className="rant-empty-hint">沒有找到含有「{activeQuery.trim()}」的貼文</p>
        ) : (
          filteredPosts.map((post) => (
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
                updatePost(() => replyRant(post.id, { userId: user.userId, nickname: user.nickname, content: replies[post.id] ?? '', ...(replyMedia[post.id] ?? EMPTY_MEDIA), parentReplyId }))
              }
              onDelete={() => updatePost(() => deleteRant(post.id, user.userId))}
              onLikedReply={loadPosts}
              onHashtagClick={(tag) => { setSearchQuery(tag); setActiveQuery(tag) }}
              currentUserId={user.userId}
            />
          ))
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
