import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ChevronLeft } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'

const FEEDBACK_EMAIL = 'support@tongpin.today'

export default function FeedbackPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [text, setText] = useState('')

  if (!user) return null

  function send() {
    const body = encodeURIComponent(text.trim())
    const subject = encodeURIComponent('同頻 Today 意見回饋')
    window.location.href = `mailto:${FEEDBACK_EMAIL}?subject=${subject}&body=${body}`
  }

  return (
    <div className="page account-page">
      <div className="detail-topbar">
        <button className="detail-back-btn" type="button" onClick={() => navigate('/settings')} aria-label="返回">
          <ChevronLeft size={22} />
        </button>
        <span className="detail-topbar-title">意見回饋</span>
      </div>

      <section className="panel" style={{ marginTop: 12 }}>
        <p className="setting-section-title">想跟我們說什麼？</p>
        <textarea
          className="profile-text-input"
          rows={6}
          style={{ resize: 'none', width: '100%' }}
          placeholder="使用上的問題、想要的功能、任何想法都可以…"
          value={text}
          onChange={(e) => setText(e.target.value)}
        />
        <button
          className="new-post-submit"
          type="button"
          style={{ marginTop: 12 }}
          disabled={!text.trim()}
          onClick={send}
        >
          送出
        </button>
        <p className="account-danger-note" style={{ marginTop: 10 }}>
          會開啟你的郵件 App 寄給 {FEEDBACK_EMAIL}。
        </p>
      </section>
    </div>
  )
}
