import { useEffect, useMemo, useState } from 'react'
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

  const fireflies = useMemo(() =>
    Array.from({ length: 12 }, (_, i) => ({
      id: i,
      top: `${10 + Math.random() * 75}%`,
      left: `${5 + Math.random() * 90}%`,
      fx: `${(Math.random() - 0.5) * 80}px`,
      fy: `${(Math.random() - 0.5) * 60}px`,
      fd: `${5 + Math.random() * 5}s`,
      delay: `${Math.random() * 6}s`,
    }))
  , [])

  return (
    <div className="page rant-page">
      {/* 螢火蟲 */}
      {fireflies.map((f) => (
        <div
          key={f.id}
          className="firefly"
          style={{ top: f.top, left: f.left, '--fx': f.fx, '--fy': f.fy, '--fd': f.fd, '--delay': f.delay } as React.CSSProperties}
        />
      ))}

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
                updatePost(() => replyRant(post.id, { nickname: user.nickname, content: replies[post.id] ?? '', ...(replyMedia[post.id] ?? EMPTY_MEDIA), parentReplyId }))
              }
              onDelete={() => updatePost(() => deleteRant(post.id))}
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
