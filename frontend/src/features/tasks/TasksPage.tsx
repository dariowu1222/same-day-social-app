import { useAuth } from '../auth/AuthContext'
import { getTasks, joinTask } from './api'
import TaskCard from './TaskCard'
import { useResource } from '../../shared/hooks/useResource'

export default function TasksPage() {
  const { user } = useAuth()
  const { data: tasks, reload } = useResource(getTasks)

  if (!user) return null

  async function handleJoin(taskId: string) {
    await joinTask(taskId, user!.userId)
    await reload()
  }

  return (
    <div className="page">
      <header className="page-header">
        <p className="eyebrow">任務</p>
        <h1>不一定要聊天，也可以先一起完成一件小事。</h1>
      </header>
      <section className="list">
        {(tasks ?? []).map((task) => (
          <TaskCard key={task.id} task={task} currentUserId={user.userId} onJoin={() => handleJoin(task.id)} />
        ))}
      </section>
    </div>
  )
}
