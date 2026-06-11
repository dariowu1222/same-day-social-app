import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import type { DemoUser } from '../App'
import { createRant } from '../api/client'
import HashtagInput from '../components/HashtagInput'
import MediaInput, { EMPTY_MEDIA, type MediaState } from '../components/MediaInput'

type Props = {
  user: DemoUser
}

export default function RantNewPostPage({ user }: Props) {
  const navigate = useNavigate()
  const [content, setContent] = useState('')
  const [mode, setMode] = useState('JUST_SAYING')
  const [hashtags, setHashtags] = useState<string[]>([])
  const [postMedia, setPostMedia] = useState<MediaState>(EMPTY_MEDIA)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  async function submitPost() {
    setSubmitting(true)
    setError('')
    try {
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

        <select value={mode} onChange={(e) => setMode(e.target.value)}>
          <option value="JUST_SAYING">只是想說</option>
          <option value="COMFORT_ME">想被安慰</option>
          <option value="GIVE_ADVICE">想聽建議</option>
          <option value="RANT_TOGETHER">想一起抱怨</option>
          <option value="DISTRACT_ME">想轉移注意力</option>
          <option value="FIND_SIMILAR">想找同類</option>
        </select>

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
