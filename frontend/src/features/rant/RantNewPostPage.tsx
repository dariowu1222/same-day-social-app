import { useRef, useState, type MouseEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { createRant } from './api'
import HashtagInput from '../../shared/ui/HashtagInput'
import PostMediaInput, { EMPTY_POST_MEDIA, type PostMediaState } from './PostMediaInput'
import { DEFAULT_RANT_MODE, RANT_MODES } from './rantModes'

export default function RantNewPostPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [content, setContent] = useState('')
  const [mode, setMode] = useState(DEFAULT_RANT_MODE)
  const [hashtags, setHashtags] = useState<string[]>([])
  const [tagsOpen, setTagsOpen] = useState(false)
  const [postMedia, setPostMedia] = useState<PostMediaState>(EMPTY_POST_MEDIA)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const textRef = useRef<HTMLTextAreaElement>(null)

  const canSubmit = content.trim() !== '' && !submitting

  async function submitPost() {
    setSubmitting(true)
    setError('')
    try {
      if (!user) return
      await createRant({ content, mode, hashtags, ...postMedia })
      navigate('/rant')
    } catch (err) {
      setError(err instanceof Error ? err.message : '發文失敗')
      setSubmitting(false)
    }
  }

  // 還沒寫字就按：按鈕晃一下、游標回到輸入框——告訴人「有收到，但還缺內容」。
  // 所以不用 disabled：disabled 的按鈕按了完全沒反應。
  function handleSubmitClick(e: MouseEvent<HTMLButtonElement>) {
    if (submitting) return
    if (canSubmit) {
      void submitPost()
      return
    }
    textRef.current?.focus()
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
    e.currentTarget.animate(
      [
        { transform: 'translateX(0)' },
        { transform: 'translateX(-5px)' },
        { transform: 'translateX(5px)' },
        { transform: 'translateX(0)' },
      ],
      { duration: 300 },
    )
  }

  return (
    <div className="page new-post-page">
      {/* 送出鈕放在標題列：以前在整頁最底下，要捲過一整排標籤才看得到，
          第一眼只有輸入框，不知道寫完要按哪裡。 */}
      <div className="new-post-header">
        <button className="new-post-back" type="button" onClick={() => navigate('/rant')} aria-label="返回">
          ‹
        </button>
        <span className="new-post-title">說說看</span>
        <button
          type="button"
          className={`keycap-btn new-post-send${canSubmit ? '' : ' is-off'}`}
          aria-disabled={!canSubmit}
          onClick={handleSubmitClick}
        >
          {submitting ? '送出中…' : '放進樹洞'}
        </button>
      </div>

      <div className="new-post-body">
        {error && <p className="notice">{error}</p>}

        <div className="post-input-block">
          <textarea
            ref={textRef}
            rows={5}
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder="今天有點卡住的事，說出來就好"
            autoFocus
          />
          <div className="post-input-toolbar">
            <PostMediaInput value={postMedia} onChange={setPostMedia} />
          </div>
        </div>

        {/* 六個選項直接攤開。以前是下拉選單，要點開才知道有哪些，
            而這一項會決定別人怎麼回你，比標籤重要，所以排在標籤前面。 */}
        <section className="new-post-section">
          <h2 className="new-post-label">
            想被怎麼回應
            <small>回應的人會看到</small>
          </h2>
          <div className="mode-chips" role="radiogroup" aria-label="想被怎麼回應">
            {RANT_MODES.map(({ key, label, Icon }) => (
              <button
                key={key}
                type="button"
                role="radio"
                aria-checked={mode === key}
                className={`mode-chip${mode === key ? ' is-on' : ''}`}
                onClick={() => setMode(key)}
              >
                <Icon size={15} strokeWidth={2.2} aria-hidden="true" />
                {label}
              </button>
            ))}
          </div>
        </section>

        <section className="new-post-section">
          <h2 className="new-post-label">
            標籤
            <button type="button" className="new-post-more" onClick={() => setTagsOpen((open) => !open)}>
              {tagsOpen ? '收起來' : '看全部'}
            </button>
          </h2>
          <HashtagInput value={hashtags} onChange={setHashtags} expanded={tagsOpen} />
        </section>
      </div>
    </div>
  )
}
