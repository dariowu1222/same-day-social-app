import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { createRant } from './api'
import HashtagInput from '../../shared/ui/HashtagInput'
import MediaInput, { EMPTY_MEDIA, type MediaState } from '../../shared/ui/MediaInput'
import ContextSelect from './ContextSelect'
import { DEFAULT_RANT_MODE } from './rantModes'

export default function RantNewPostPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [content, setContent] = useState('')
  const [mode, setMode] = useState(DEFAULT_RANT_MODE)
  const [hashtags, setHashtags] = useState<string[]>([])
  const [postMedia, setPostMedia] = useState<MediaState>(EMPTY_MEDIA)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  async function submitPost() {
    setSubmitting(true)
    setError('')
    try {
      if (!user) return
      await createRant({ nickname: user.nickname, content, mode, hashtags, ...postMedia })
      navigate('/rant')
    } catch (err) {
      setError(err instanceof Error ? err.message : '發文失敗')
      setSubmitting(false)
    }
  }

  return (
    <div className="page">
      <div className="new-post-header">
        <button className="new-post-back" onClick={() => navigate('/rant')}>
          <ArrowLeft size={20} />
        </button>
        <span className="new-post-title">說說看</span>
      </div>

      <div className="new-post-body">
        <div className="post-input-block">
          <textarea
            rows={6}
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder="今天有點卡住的事，說出來就好…"
            autoFocus
          />
          <div className="post-input-toolbar">
            <MediaInput value={postMedia} onChange={setPostMedia} />
          </div>
        </div>

        <HashtagInput value={hashtags} onChange={setHashtags} />

        <ContextSelect value={mode} onChange={setMode} />

        <button
          className="new-post-submit"
          onClick={submitPost}
          disabled={!content.trim() || submitting}
        >
          {submitting ? '送出中…' : '放進樹洞'}
        </button>

        {error && <p className="notice">{error}</p>}
      </div>
    </div>
  )
}
