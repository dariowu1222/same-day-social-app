import type { MatchResult } from '../api/client'

type Props = {
  match: MatchResult
  onChat: (match: MatchResult) => void
}

export default function MatchCard({ match, onChat }: Props) {
  return (
    <article className="card">
      <div className="card-title-row">
        <h3>{match.nickname}</h3>
        <span className="score">{match.matchScore}</span>
      </div>
      <p>{match.reason}</p>
      <p className="muted">{match.todaySummary}</p>
      <div className="tag-row">
        {match.sharedTags.map((tag) => (
          <span key={tag} className="tag">
            {tag}
          </span>
        ))}
      </div>
      <blockquote>{match.icebreaker}</blockquote>
      <button className="secondary" onClick={() => onChat(match)}>
        想聊聊
      </button>
    </article>
  )
}
