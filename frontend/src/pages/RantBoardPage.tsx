import { useEffect, useState } from 'react'
import type { DemoUser } from '../App'
import { createRant, getRants, replyRant, reportRant, understandRant, type RantPost } from '../api/client'
import RantPostCard from '../components/RantPostCard'

type Props = { user: DemoUser }
type PageView = 'feed' | 'compose' | 'choosing'

const responseModes = [
  { key: 'JUST_SAYING',   label: '聽著就好',     emoji: '🌙' },
  { key: 'COMFORT_ME',    label: '給我一點溫暖', emoji: '🫂' },
  { key: 'GIVE_ADVICE',   label: '幫我想想辦法', emoji: '💡' },
  { key: 'RANT_TOGETHER', label: '陪我一起氣',   emoji: '😤' },
]

export default function RantBoardPage({ user }: Props) {
  const [posts, setPosts] = useState<RantPost[]>([])
  const [view, setView] = useState<PageView>('feed')
  const [content, setContent] = useState('')
  const [isAnonymous, setIsAnonymous] = useState(true)
  const [visibility, setVisibility] = useState<'ALL' | 'FRIENDS'>('ALL')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [pendingContent, setPendingContent] = useState('')

  useEffect(() => { loadPosts() }, [])

  async function loadPosts() {
    const response = await getRants(user.userId)
    setPosts(response.data)
  }

  function handleFirstSubmit() {
    if (!content.trim()) return
    setPendingContent(content)
    setView('choosing')
  }

  async function handleFinalSubmit(mode: string) {
    setIsSubmitting(true)
    try {
      const nickname = isAnonymous ? '匿名' : user.nickname
      await createRant({ userId: user.userId, nickname, content: pendingContent, mode, visibility })
      setContent('')
      setPendingContent('')
      setView('feed')
      await loadPosts()
    } catch {
      setView('compose')
    } finally {
      setIsSubmitting(false)
    }
  }

  async function updatePost(action: () => Promise<unknown>) {
    await action()
    await loadPosts()
  }

  return (
    <div className="page rant-page">
      {/* 螢火蟲 */}
      <div className="rant-fireflies" aria-hidden>
        {[...Array(6)].map((_, i) => (
          <span key={i} className={`rant-firefly rant-firefly-${i + 1}`} />
        ))}
      </div>

      {/* ── 貼文列表頁 ── */}
      {view === 'feed' && (
        <>
          <header className="rant-header">
            <h1 className="rant-title">樹洞</h1>
          </header>
          <section className="rant-list">
            {posts.length === 0 ? (
              <div className="rant-empty-state">
                <p className="rant-empty">樹洞裡還很安靜。</p>
                <p className="rant-empty-sub">點右下角的 + 說說今天的事？</p>
              </div>
            ) : (
              posts.map((post) => (
                <RantPostCard
                  key={post.id}
                  post={post}
                  currentUserId={user.userId}
                  onUnderstand={() => updatePost(() => understandRant(post.id, user.userId))}
                  onReport={() => updatePost(() => reportRant(post.id))}
                  onReply={(content, parentReplyId) => updatePost(() => replyRant(post.id, {
                    userId: user.userId,
                    nickname: isAnonymous ? '匿名' : user.nickname,
                    content,
                    parentReplyId,
                  }))}
                />
              ))
            )}
          </section>

          {/* 浮動 + 按鈕 */}
          <button className="rant-fab" onClick={() => setView('compose')} aria-label="新增貼文">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
              <path d="M12 5v14M5 12h14" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round"/>
            </svg>
          </button>
        </>
      )}

      {/* ── 撰寫頁 ── */}
      {view === 'compose' && (
        <div className="rant-compose-view">
          <header className="rant-compose-header">
            <button className="rant-back-btn" onClick={() => setView('feed')}>
              <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
                <path d="M12.5 15L7.5 10L12.5 5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"/>
              </svg>
              返回
            </button>
            <h2 className="rant-compose-title">說說今天卡在心裡的事。</h2>
          </header>

          <section className="rant-compose">
            <textarea
              className="rant-textarea"
              rows={6}
              value={content}
              onChange={(e) => setContent(e.target.value)}
              placeholder="今天有點卡住的事，說出來就好…"
            />

            {/* 可見度 toggle */}
            <div className="rant-visibility">
              <span className="rant-visibility-label">誰可以看</span>
              <div className="rant-visibility-toggle">
                <button
                  className={`rant-vis-btn${visibility === 'ALL' ? ' active' : ''}`}
                  onClick={() => setVisibility('ALL')}
                >🌍 所有人</button>
                <button
                  className={`rant-vis-btn${visibility === 'FRIENDS' ? ' active' : ''}`}
                  onClick={() => setVisibility('FRIENDS')}
                >🔒 朋友</button>
              </div>
            </div>

            {/* 匿名 */}
            <label className="rant-anonymous">
              <input type="checkbox" checked={isAnonymous} onChange={(e) => setIsAnonymous(e.target.checked)} />
              <span>匿名發布</span>
              <span className="rant-anonymous-hint">
                {isAnonymous ? '別人看不到你的名字' : `以「${user.nickname}」發布`}
              </span>
            </label>

            <button className="rant-submit-btn" onClick={handleFirstSubmit} disabled={!content.trim()}>
              放進樹洞
            </button>
          </section>
        </div>
      )}

      {/* ── 選擇回應模式 ── */}
      {view === 'choosing' && (
        <div className="rant-compose-view">
          <div className="rant-compose">
            <div className="rant-choose-phase">
              <p className="rant-choose-title">說出來了。</p>
              <p className="rant-choose-sub">你希望大家怎麼回應你？</p>
              <div className="rant-response-modes">
                {responseModes.map((m, i) => (
                  <button
                    key={m.key}
                    className="rant-response-btn"
                    style={{ animationDelay: `${i * 80}ms` }}
                    onClick={() => handleFinalSubmit(m.key)}
                    disabled={isSubmitting}
                  >
                    <span className="rant-response-emoji">{m.emoji}</span>
                    <span>{m.label}</span>
                  </button>
                ))}
              </div>
              <button className="rant-skip-btn" onClick={() => handleFinalSubmit('JUST_SAYING')} disabled={isSubmitting}>
                跳過，直接送出
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
