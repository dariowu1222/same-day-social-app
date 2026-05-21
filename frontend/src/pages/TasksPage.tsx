import { useEffect, useState } from 'react'
import type { DemoUser } from '../App'
import { getTasks, joinTask, type SocialTask } from '../api/client'
import TaskCard from '../components/TaskCard'

type Props = {
  user: DemoUser
}

export default function TasksPage({ user }: Props) {
  const [tasks, setTasks] = useState<SocialTask[]>([])

  useEffect(() => {
    loadTasks()
  }, [])

  async function loadTasks() {
    const response = await getTasks()
    setTasks(response.data)
  }

  async function handleJoin(taskId: string) {
    await joinTask(taskId, user.userId)
    await loadTasks()
  }

  return (
    <div className="page">
      <header className="page-header">
        <p className="eyebrow">任務</p>
        <h1>不一定要聊天，也可以先一起完成一件小事。</h1>
      </header>
      <section className="list">
        {tasks.map((task) => (
          <TaskCard key={task.id} task={task} currentUserId={user.userId} onJoin={() => handleJoin(task.id)} />
        ))}
      </section>
    </div>
  )
}
