import { useEffect, useState } from 'react'
import { Routes, Route, useNavigate, useLocation } from 'react-router-dom'
import { Capacitor } from '@capacitor/core'
import { StatusBar, Style } from '@capacitor/status-bar'
import BottomNav, { type PageKey } from './components/BottomNav'
import LoginPage from './components/LoginPage'
import OnboardingOverlay from './components/OnboardingOverlay'
import ThemeToggle from './components/ThemeToggle'
import ThemeTransitionOverlay, { GlobalNightEffects } from './components/ThemeTransitionOverlay'
import { useTheme } from './context/ThemeContext'
import TodayPage from './pages/TodayPage'
import MatchResultPage from './pages/MatchResultPage'
import RantBoardPage from './pages/RantBoardPage'
import RantDetailPage from './pages/RantDetailPage'
import RantNewPostPage from './pages/RantNewPostPage'
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
  const navigate = useNavigate()
  const location = useLocation()
  const [showMatchResult, setShowMatchResult] = useState(false)
  const [selectedMoodLabel, setSelectedMoodLabel] = useState<string>('')
  const [user, setUser] = useState<DemoUser | null>(null)
  const [onboardingDone, setOnboardingDone] = useState(true)
  const { mode } = useTheme()

  useEffect(() => {
    const isDark = mode === 'night'
    document.documentElement.setAttribute('data-theme', isDark ? 'dark' : '')
    // PWA / Chrome on Android
    const meta = document.querySelector('meta[name="theme-color"]')
    if (meta) meta.setAttribute('content', isDark ? '#050208' : '#faf7f2')
    // 原生 iOS / Android Capacitor status bar 圖示顏色
    if (Capacitor.isNativePlatform()) {
      StatusBar.setStyle({ style: isDark ? Style.Dark : Style.Light }).catch(() => {})
    }
  }, [mode])

  const pathToPage: Record<string, PageKey> = {
    '/': 'today', '/rant': 'rant', '/tasks': 'tasks', '/chat': 'chat', '/profile': 'profile',
  }
  const activePage: PageKey = pathToPage[location.pathname]
    ?? (location.pathname.startsWith('/rant') ? 'rant' : 'today')

  function setActivePage(page: PageKey) {
    const pageToPath: Record<PageKey, string> = {
      today: '/', rant: '/rant', tasks: '/tasks', chat: '/chat', profile: '/profile',
    }
    navigate(pageToPath[page])
  }

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
    navigate('/profile')
  }

  if (!user) {
    return <LoginPage onAuthenticated={handleAuthenticated} />
  }

  // 只有發文頁隱藏底部導覽列，貼文詳細頁照常顯示
  const isDetail = location.pathname === '/rant/new'

  return (
    <>
      <main className="app-shell">
        <Routes>
          <Route path="/" element={
            !showMatchResult
              ? <TodayPage user={user} onGoToMatches={(label) => { setSelectedMoodLabel(label); setShowMatchResult(true) }} />
              : <MatchResultPage moodLabel={selectedMoodLabel} onBack={() => setShowMatchResult(false)} onGoToRant={() => { setShowMatchResult(false); navigate('/rant') }} />
          } />
          <Route path="/rant" element={<RantBoardPage user={user} />} />
          <Route path="/rant/new" element={<RantNewPostPage user={user} />} />
          <Route path="/rant/:rantId" element={<RantDetailPage user={user} />} />
          <Route path="/tasks" element={<TasksPage user={user} />} />
          <Route path="/chat" element={<ChatPage user={user} />} />
          <Route path="/profile" element={<ProfilePage user={user} setUser={setUser} />} />
        </Routes>
        {!isDetail && <BottomNav activePage={activePage} onChange={setActivePage} />}
      </main>
      {mode === 'night' && <GlobalNightEffects />}
      <ThemeTransitionOverlay />
      {!onboardingDone && (
        <OnboardingOverlay user={user} onComplete={handleOnboardingComplete} />
      )}
    </>
  )
}

export default App
