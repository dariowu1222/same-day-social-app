import { tagLabel } from '../../shared/lib/tagLabels'
import type { MatchResult } from './types'

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
        {/* key 用代號、顯示用中文：代號才是穩定的識別，文案會改 */}
        {match.sharedTags.map((tag) => (
          <span key={tag} className="tag">
            {tagLabel(tag)}
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
