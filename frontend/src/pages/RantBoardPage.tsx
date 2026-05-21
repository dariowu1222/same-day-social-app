import { useEffect, useState } from 'react'
import type { DemoUser } from '../App'
import { createRant, getRants, replyRant, reportRant, understandRant, type RantPost } from '../api/client'
import RantPostCard from '../components/RantPostCard'

type Props = {
  user: DemoUser
}

export default function RantBoardPage({ user }: Props) {
  const [posts, setPosts] = useState<RantPost[]>([])
  const [content, setContent] = useState('')
  const [mode, setMode] = useState('JUST_SAYING')
  const [replies, setReplies] = useState<Record<string, string>>({})
  const [message, setMessage] = useState('')

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
      const response = await createRant({ userId: user.userId, nickname: user.nickname, content, mode })
      setContent('')
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
      <section className="panel">
        <textarea rows={5} value={content} onChange={(event) => setContent(event.target.value)} placeholder="今天有點卡住的事..." />
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
      <section className="list">
        {posts.map((post) => (
          <RantPostCard
            key={post.id}
            post={post}
            replyText={replies[post.id] ?? ''}
            onReplyTextChange={(value) => setReplies({ ...replies, [post.id]: value })}
            onUnderstand={() => updatePost(() => understandRant(post.id))}
            onReport={() => updatePost(() => reportRant(post.id))}
            onReply={() =>
              updatePost(() => replyRant(post.id, { userId: user.userId, nickname: user.nickname, content: replies[post.id] ?? '' }))
            }
          />
        ))}
      </section>
    </div>
  )
}
