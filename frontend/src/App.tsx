import { useEffect, useState } from 'react'
import BottomNav, { type PageKey } from './components/BottomNav'
import LoginPage from './components/LoginPage'
import OnboardingOverlay from './components/OnboardingOverlay'
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

function onboardingKey(userId: string) {
  return `same-day-onboarding-${userId}`
}

function App() {
  const [activePage, setActivePage] = useState<PageKey>('today')
  const [user, setUser] = useState<DemoUser | null>(null)
  const [onboardingDone, setOnboardingDone] = useState(true)

  useEffect(() => {
    const saved = localStorage.getItem('same-day-demo-user') ?? sessionStorage.getItem('same-day-demo-user')
    if (saved) {
      const nextUser: DemoUser = JSON.parse(saved)
      setUser(nextUser)
      setOnboardingDone(!!localStorage.getItem(onboardingKey(nextUser.userId)))
    }
  }, [])

  function handleAuthenticated(nextUser: DemoUser, remember: boolean) {
    if (remember) {
      localStorage.setItem('same-day-demo-user', JSON.stringify(nextUser))
      sessionStorage.removeItem('same-day-demo-user')
    } else {
      sessionStorage.setItem('same-day-demo-user', JSON.stringify(nextUser))
      localStorage.removeItem('same-day-demo-user')
    }
    setUser(nextUser)
    setOnboardingDone(!!localStorage.getItem(onboardingKey(nextUser.userId)))
  }

  function handleOnboardingComplete() {
    if (user) localStorage.setItem(onboardingKey(user.userId), '1')
    setOnboardingDone(true)
  }

  if (!user) {
    return <LoginPage onAuthenticated={handleAuthenticated} />
  }

  return (
    <>
      <main className="app-shell">
        {activePage === 'today' && <TodayPage user={user} />}
        {activePage === 'rant' && <RantBoardPage user={user} />}
        {activePage === 'tasks' && <TasksPage user={user} />}
        {activePage === 'chat' && <ChatPage user={user} />}
        {activePage === 'profile' && <ProfilePage user={user} setUser={setUser} />}
        <BottomNav activePage={activePage} onChange={setActivePage} />
      </main>
      {!onboardingDone && (
        <OnboardingOverlay user={user} onComplete={handleOnboardingComplete} />
      )}
    </>
  )
}

export default App
