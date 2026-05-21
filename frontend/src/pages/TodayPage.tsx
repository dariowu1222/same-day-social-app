import { useState } from 'react'
import type { DemoUser } from '../App'
import { createChatRoom, createTodayEntry, getTodayMatches, type MatchResult, type TodayAnalysis } from '../api/client'
import MatchCard from '../components/MatchCard'
import TodayEntryForm from '../components/TodayEntryForm'

type Props = {
  user: DemoUser
}

export default function TodayPage({ user }: Props) {
  const [content, setContent] = useState('')
  const [responseMode, setResponseMode] = useState('COMFORT_ME')
  const [visibility, setVisibility] = useState('MATCH_ONLY')
  const [analysis, setAnalysis] = useState<TodayAnalysis | null>(null)
  const [matches, setMatches] = useState<MatchResult[]>([])
  const [message, setMessage] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function submitToday() {
    setIsSubmitting(true)
    setMessage('')
    try {
      const entryResponse = await createTodayEntry({ userId: user.userId, content, responseMode, visibility })
      setAnalysis(entryResponse.data.analysis)
      const matchResponse = await getTodayMatches(user.userId)
      setMatches(matchResponse.data)
      setMessage(matchResponse.data.length === 0 ? '已記下今天。現在還沒有足夠同頻的人，可以晚點再看看。' : '')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : '送出失敗')
    } finally {
      setIsSubmitting(false)
    }
  }

  async function startChat(match: MatchResult) {
    await createChatRoom({
      userIds: [user.userId, match.matchedUserId],
      sourceType: 'TODAY_MATCH',
      sourceId: match.matchId,
    })
    setMessage('聊天室已建立，可以到「聊天」頁慢慢說。')
  }

  return (
    <div className="page">
      <header className="page-header">
        <p className="eyebrow">今日</p>
        <h1>今天發生了什麼？</h1>
        <p>我們會幫你找到今天也有類似感受的人。</p>
      </header>
      <TodayEntryForm
        content={content}
        responseMode={responseMode}
        visibility={visibility}
        isSubmitting={isSubmitting}
        onContentChange={setContent}
        onResponseModeChange={setResponseMode}
        onVisibilityChange={setVisibility}
        onSubmit={submitToday}
      />
      {analysis && (
        <section className="panel">
          <h2>今日分析</h2>
          <div className="tag-row">
            <span className="tag">{analysis.eventType}</span>
            {analysis.emotionTags.map((tag) => (
              <span key={tag} className="tag">
                {tag}
              </span>
            ))}
            {analysis.valueTags.map((tag) => (
              <span key={tag} className="tag">
                {tag}
              </span>
            ))}
          </div>
        </section>
      )}
      {message && <p className="notice">{message}</p>}
      <section className="list">
        {matches.map((match) => (
          <MatchCard key={match.matchId} match={match} onChat={startChat} />
        ))}
      </section>
    </div>
  )
}
