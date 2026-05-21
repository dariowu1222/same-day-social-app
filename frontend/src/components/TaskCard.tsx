import type { SocialTask } from '../api/client'

type Props = {
  task: SocialTask
  currentUserId: string
  onJoin: () => void
}

export default function TaskCard({ task, currentUserId, onJoin }: Props) {
  const joined = task.participantUserIds.includes(currentUserId)

  return (
    <article className="card">
      <div className="card-title-row">
        <h3>{task.title}</h3>
        <span className="tag">{task.category}</span>
      </div>
      <p>{task.description}</p>
      <p className="muted">
        {task.duration} · {task.difficulty} · {task.participantUserIds.length}/{task.participantLimit}
      </p>
      <button className={joined ? 'ghost' : 'secondary'} onClick={onJoin} disabled={joined}>
        {joined ? '已參加' : '我想參加'}
      </button>
    </article>
  )
}
