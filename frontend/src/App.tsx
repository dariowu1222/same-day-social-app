import { useEffect, useState } from 'react'
import BottomNav, { type PageKey } from './components/BottomNav'
import LoginPage from './components/LoginPage'
import TodayPage from './pages/TodayPage'
import RantBoardPage from './pages/RantBoardPage'
import TasksPage from './pages/TasksPage'
import ChatPage from './pages/ChatPage'
import ProfilePage from './pages/ProfilePage'

export type DemoUser = {
  userId: string
  nickname: string
  email?: string
}

function App() {
  const [activePage, setActivePage] = useState<PageKey>('today')
  const [user, setUser] = useState<DemoUser | null>(null)

  useEffect(() => {
    const saved = localStorage.getItem('same-day-demo-user')
    if (saved) {
      setUser(JSON.parse(saved))
    }
  }, [])

  function handleAuthenticated(nextUser: DemoUser) {
    localStorage.setItem('same-day-demo-user', JSON.stringify(nextUser))
    setUser(nextUser)
  }

  if (!user) {
    return (
      <LoginPage
        onAuthenticated={handleAuthenticated}
      />
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
