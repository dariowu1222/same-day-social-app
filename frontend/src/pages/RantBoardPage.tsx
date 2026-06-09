import { useEffect, useState } from 'react'
import { Plus, X, ChevronDown } from 'lucide-react'
import type { DemoUser } from '../App'
import { createRant, deleteRant, getRants, replyRant, reportRant, understandRant, type RantPost } from '../api/client'
import HashtagInput from '../components/HashtagInput'
import MediaInput, { EMPTY_MEDIA, type MediaState } from '../components/MediaInput'
import RantPostCard from '../components/RantPostCard'
import { Search } from 'lucide-react'

type Props = {
  user: DemoUser
}

export default function RantBoardPage({ user }: Props) {
  const [posts, setPosts] = useState<RantPost[]>([])
  const [content, setContent] = useState('')
  const [mode, setMode] = useState('JUST_SAYING')
  const [hashtags, setHashtags] = useState<string[]>([])
  const [postMedia, setPostMedia] = useState<MediaState>(EMPTY_MEDIA)
  const [replies, setReplies] = useState<Record<string, string>>({})
  const [replyMedia, setReplyMedia] = useState<Record<string, MediaState>>({})
  const [message, setMessage] = useState('')
  const [showForm, setShowForm] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')

  const filteredPosts = searchQuery.trim()
    ? posts.filter((post) => {
        const q = searchQuery.trim().toLowerCase()
        return (
          post.content.toLowerCase().includes(q) ||
          post.hashtags?.some((tag) => tag.toLowerCase().includes(q))
        )
      })
    : posts

  useEffect(() => {
    loadPosts()
  }, [])

  async function loadPosts() {
    const response = await getRants()
    setPosts(response.data)
  }

  async function submitPost() {
    setMessage('')
    try {
      const response = await createRant({ userId: user.userId, nickname: user.nickname, content, mode, hashtags, ...postMedia })
      setContent('')
      setHashtags([])
      setPostMedia(EMPTY_MEDIA)
      setMessage(response.warning ?? '已放進樹洞。')
      await loadPosts()
    } catch (error) {
      setMessage(error instanceof Error ? error.message : '發文失敗')
    }
  }

  async function updatePost(action: () => Promise<unknown>) {
    await action()
    await loadPosts()
  }

  return (
    <div className="page">
      {/* ── 搜尋欄（sticky，在最上方）── */}
      <div className="rant-search-bar">
        <div className="rant-search-inner">
          <Search size={15} className="rant-search-icon" />
          <input
            className="rant-search-input"
            placeholder="搜尋…"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
          {searchQuery && (
            <button className="rant-search-clear" onClick={() => setSearchQuery('')}>
              <X size={14} />
            </button>
          )}
        </div>
        {searchQuery.trim() && (
          <span className="rant-search-count">
            {filteredPosts.length > 0 ? `${filteredPosts.length} 則` : '無符合貼文'}
          </span>
        )}
      </div>

      {/* 標題（一行）*/}
      <p className="rant-board-title">這裡可以說說今天不太想放在心裡的事</p>

      {/* 發文區（收合）*/}
      {showForm && (
        <section className="panel">
          <div className="post-input-block">
            <textarea rows={5} value={content} onChange={(event) => setContent(event.target.value)} placeholder="今天有點卡住的事，說出來就好…" />
            <div className="post-input-toolbar">
              <MediaInput value={postMedia} onChange={setPostMedia} />
            </div>
          </div>
          <HashtagInput value={hashtags} onChange={setHashtags} />
          <select value={mode} onChange={(event) => setMode(event.target.value)}>
            <option value="JUST_SAYING">只是想說</option>
            <option value="COMFORT_ME">想被安慰</option>
            <option value="GIVE_ADVICE">想聽建議</option>
            <option value="RANT_TOGETHER">想一起抱怨</option>
            <option value="DISTRACT_ME">想轉移注意力</option>
            <option value="FIND_SIMILAR">想找同類</option>
          </select>
          <button onClick={submitPost} disabled={!content.trim()}>
            放進樹洞
          </button>
          {message && <p className="notice">{message}</p>}
        </section>
      )}

      <section className="list">
        {filteredPosts.length === 0 && searchQuery.trim() ? (
          <p className="rant-empty-hint">沒有找到含有「{searchQuery.trim()}」的貼文</p>
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
              onHashtagClick={(tag) => setSearchQuery(tag)}
              currentUserId={user.userId}
            />
          ))
        )}
      </section>

      {/* FAB：右下角發文按鈕 */}
      <button
        className={`rant-fab${showForm ? ' rant-fab-open' : ''}`}
        onClick={() => setShowForm((v) => !v)}
        aria-label={showForm ? '收起' : '說說看'}
      >
        {showForm ? <ChevronDown size={22} /> : <Plus size={24} />}
      </button>
    </div>
  )
}
