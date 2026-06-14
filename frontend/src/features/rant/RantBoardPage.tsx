import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Plus, X, Search } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { deleteRant, getRants, replyRant, reportRant, understandRant } from './api'
import { EMPTY_MEDIA, type MediaState } from '../../shared/ui/MediaInput'
import RantPostCard from './RantPostCard'
import { useResource } from '../../shared/hooks/useResource'

export default function RantBoardPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const { data: posts, reload: loadPosts } = useResource(getRants)
  const [replies, setReplies] = useState<Record<string, string>>({})
  const [replyMedia, setReplyMedia] = useState<Record<string, MediaState>>({})
  const [searchQuery, setSearchQuery] = useState('')
  const [activeQuery, setActiveQuery] = useState('')

  const filteredPosts = activeQuery.trim()
    ? (posts ?? []).filter((post) => {
        const q = activeQuery.trim().toLowerCase()
        return (
          post.content.toLowerCase().includes(q) ||
          post.hashtags?.some((tag) => tag.toLowerCase().includes(q))
        )
      })
    : (posts ?? [])

  function commitSearch() {
    setActiveQuery(searchQuery)
  }

  function clearSearch() {
    setSearchQuery('')
    setActiveQuery('')
  }

  async function updatePost(action: () => Promise<unknown>) {
    await action()
    await loadPosts()
  }

  if (!user) return null

  return (
    <div className="page rant-page">

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
