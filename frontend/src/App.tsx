import { useEffect, useState } from 'react'
import { demoLogin } from './api/client'
import BottomNav, { type PageKey } from './components/BottomNav'
import TodayPage from './pages/TodayPage'
import RantBoardPage from './pages/RantBoardPage'
import TasksPage from './pages/TasksPage'
import ChatPage from './pages/ChatPage'
import ProfilePage from './pages/ProfilePage'

export type DemoUser = {
  userId: string
  nickname: string
}

function App() {
  const [activePage, setActivePage] = useState<PageKey>('today')
  const [user, setUser] = useState<DemoUser | null>(null)
  const [nickname, setNickname] = useState('Dario')
  const [isLoading, setIsLoading] = useState(false)

  useEffect(() => {
    const saved = localStorage.getItem('same-day-demo-user')
    if (saved) {
      setUser(JSON.parse(saved))
    }
  }, [])

  async function handleLogin() {
    setIsLoading(true)
    try {
      const result = await demoLogin(nickname)
      localStorage.setItem('same-day-demo-user', JSON.stringify(result))
      setUser(result)
    } finally {
      setIsLoading(false)
    }
  }

  if (!user) {
    return (
      <main className="app-shell auth-shell">
        <section className="hero-panel">
          <p className="eyebrow">同頻 Today</p>
          <h1>今天發生了一件事，我想遇到一個剛好懂的人。</h1>
          <p>先用暱稱建立 Demo user，不需要真名，也不需要精準位置。</p>
        </section>
        <section className="panel">
          <label>
            暱稱
            <input value={nickname} onChange={(event) => setNickname(event.target.value)} />
          </label>
          <button onClick={handleLogin} disabled={isLoading}>
            {isLoading ? '建立中...' : '開始今天'}
          </button>
        </section>
      </main>
    )
  }

  return (
    <main className="app-shell">
      {activePage === 'today' && <TodayPage user={user} />}
      {activePage === 'rant' && <RantBoardPage user={user} />}
      {activePage === 'tasks' && <TasksPage user={user} />}
      {activePage === 'chat' && <ChatPage user={user} />}
      {activePage === 'profile' && <ProfilePage user={user} setUser={setUser} />}
      <BottomNav activePage={activePage} onChange={setActivePage} />
    </main>
  )
}

export default App
