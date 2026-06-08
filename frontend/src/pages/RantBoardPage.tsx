import { useEffect, useState } from 'react'
import { PenLine, ChevronDown } from 'lucide-react'
import type { DemoUser } from '../App'
import { createRant, deleteRant, getRants, replyRant, reportRant, understandRant, type RantPost } from '../api/client'
import HashtagInput from '../components/HashtagInput'
import MediaInput, { EMPTY_MEDIA, type MediaState } from '../components/MediaInput'
import RantPostCard from '../components/RantPostCard'

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
      <header className="page-header">
        <p className="eyebrow">樹洞</p>
        <h1>這裡可以說說今天不太想放在心裡的事。</h1>
        <p>可以抱怨，但不要攻擊、肉搜或公開他人個資。</p>
      </header>
      {/* 發文區 toggle */}
      <div className="rant-compose-toggle">
        <button
          className={`rant-compose-trigger${showForm ? ' open' : ''}`}
          onClick={() => setShowForm((v) => !v)}
        >
          {showForm ? (
            <><ChevronDown size={16} /> 收起</>
          ) : (
            <><PenLine size={16} /> 說說看</>
          )}
        </button>
      </div>

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
              updatePost(() => replyRant(post.id, { userId: user.userId, nickname: user.nickname, content: replies[post.id] ?? '', ...(replyMedia[post.id] ?? EMPTY_MEDIA), parentReplyId }))
            }
            onDelete={() => updatePost(() => deleteRant(post.id, user.userId))}
            currentUserId={user.userId}
          />
        ))}
      </section>
    </div>
  )
}
