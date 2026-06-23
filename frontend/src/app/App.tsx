import { useEffect, useState } from 'react'
import { Routes, Route, useNavigate, useLocation } from 'react-router-dom'
import { Capacitor } from '@capacitor/core'
import BottomNav, { type PageKey } from '../shared/ui/BottomNav'
import ThemeTransitionOverlay, { GlobalNightEffects } from '../shared/ui/ThemeTransitionOverlay'
import { useTheme } from '../shared/theme/ThemeContext'
import { onboardingKey } from '../shared/lib/storageKeys'
import { useAuth } from '../features/auth/AuthContext'
import type { DemoUser } from '../features/auth/types'
import LoginPage from '../features/auth/LoginPage'
import OnboardingOverlay from '../features/profile/OnboardingOverlay'
import TodayPage from '../features/today/TodayPage'
import MatchResultPage from '../features/match/MatchResultPage'
import RantBoardPage from '../features/rant/RantBoardPage'
import RantDetailPage from '../features/rant/RantDetailPage'
import RantNewPostPage from '../features/rant/RantNewPostPage'
import TasksPage from '../features/tasks/TasksPage'
import ChatPage from '../features/chat/ChatPage'
import ProfilePage from '../features/profile/ProfilePage'
import AccountCenterPage from '../features/profile/AccountCenterPage'
import SecurityCenterPage from '../features/profile/SecurityCenterPage'

function App() {
  const navigate = useNavigate()
  const location = useLocation()
  const { user, authenticate } = useAuth()
  const [showMatchResult, setShowMatchResult] = useState(false)
  const [selectedMoodLabel, setSelectedMoodLabel] = useState<string>('')
  const [onboardingDone, setOnboardingDone] = useState(true)
  const { mode } = useTheme()

  useEffect(() => {
    const isDark = mode === 'night'
    document.documentElement.setAttribute('data-theme', isDark ? 'dark' : '')
    // PWA / Chrome on Android
    const meta = document.querySelector('meta[name="theme-color"]')
    if (meta) meta.setAttribute('content', isDark ? '#050208' : '#faf7f2')
    // 原生 iOS / Android Capacitor status bar 圖示顏色（動態 import，網頁版不需要）
    if (Capacitor.isNativePlatform()) {
      import('@capacitor/status-bar').then(({ StatusBar, Style }) => {
        StatusBar.setStyle({ style: isDark ? Style.Dark : Style.Light }).catch(() => {})
      }).catch(() => {})
    }
  }, [mode])

  // 使用者狀態與 storage 已由 AuthContext 集中管理；這裡只依使用者推算是否看過教學。
  useEffect(() => {
    if (user) setOnboardingDone(!!localStorage.getItem(onboardingKey(user.userId)))
  }, [user])

  const pathToPage: Record<string, PageKey> = {
    '/': 'today', '/rant': 'rant', '/tasks': 'tasks', '/chat': 'chat', '/profile': 'profile',
  }
  const activePage: PageKey = pathToPage[location.pathname]
    ?? (location.pathname.startsWith('/rant') ? 'rant'
      : location.pathname.startsWith('/account') ? 'profile'
      : location.pathname.startsWith('/security') ? 'profile'
      : 'today')

  function setActivePage(page: PageKey) {
    const pageToPath: Record<PageKey, string> = {
      today: '/', rant: '/rant', tasks: '/tasks', chat: '/chat', profile: '/profile',
    }
    // 重選目前所在的 tab：發事件讓該頁自行處理（例如關閉「我的」的預覽覆蓋層）
    if (page === activePage) {
      window.dispatchEvent(new Event(`nav-reselect-${page}`))
    }
    navigate(pageToPath[page])
  }

  function handleAuthenticated(nextUser: DemoUser, remember: boolean) {
    authenticate(nextUser, remember)
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
              ? <TodayPage onGoToMatches={(label) => { setSelectedMoodLabel(label); setShowMatchResult(true) }} />
              : <MatchResultPage moodLabel={selectedMoodLabel} onBack={() => setShowMatchResult(false)} onGoToRant={() => { setShowMatchResult(false); navigate('/rant') }} />
          } />
          <Route path="/rant" element={<RantBoardPage />} />
          <Route path="/rant/new" element={<RantNewPostPage />} />
          <Route path="/rant/:rantId" element={<RantDetailPage />} />
          <Route path="/tasks" element={<TasksPage />} />
          <Route path="/chat" element={<ChatPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/account" element={<AccountCenterPage />} />
          <Route path="/security" element={<SecurityCenterPage />} />
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
