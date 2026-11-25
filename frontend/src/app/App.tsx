import { Suspense, useEffect, useState } from 'react'
import { Routes, Route, useNavigate, useLocation } from 'react-router-dom'
import { Capacitor } from '@capacitor/core'
import BottomNav, { type PageKey } from '../shared/ui/BottomNav'
import ThemeTransitionOverlay, { GlobalNightEffects } from '../shared/ui/ThemeTransitionOverlay'
import { useTheme } from '../shared/theme/ThemeContext'
import { onboardingKey } from '../shared/lib/storageKeys'
import { useAuth } from '../features/auth/AuthContext'
import { useChatRealtime } from '../features/chat/ChatRealtimeContext'
import { ensureSocialInit } from '../features/auth/googleAuth'
import { syncPushRegistration, onPushOpened } from '../shared/push/push'
import type { DemoUser } from '../features/auth/types'
import { lazyPage } from '../shared/lib/lazyPage'
import RouteFallback from '../shared/ui/RouteFallback'
import LoginPage from '../features/auth/LoginPage'
import ConsentGate from '../features/auth/ConsentGate'
import TodayPage from '../features/today/TodayPage'

// ── 分頁各自一包，用到才抓 ──
//
// 直接 import 的只剩「第一眼」：沒登入看到的 LoginPage、登入後看到的 TodayPage。
// 其餘每一頁各自打包（見 lazyPage）。以前 17 支頁面全部靜態 import，登入畫面
// 出現之前就要先下載、解析整個 App。
//
// 切分頁不會因此變慢：下面有一個 effect 會在第一個畫面畫完之後，趁空檔把這些包
// 先抓好（PAGES 就是那份清單）。
// 任務功能暫時下架（後端保留），TasksPage 不在這裡。
const pages = {
  onboarding: lazyPage(() => import('../features/profile/OnboardingOverlay')),
  match: lazyPage(() => import('../features/match/MatchResultPage')),
  rantBoard: lazyPage(() => import('../features/rant/RantBoardPage')),
  rantSearch: lazyPage(() => import('../features/rant/RantSearchPage')),
  rantDetail: lazyPage(() => import('../features/rant/RantDetailPage')),
  rantNew: lazyPage(() => import('../features/rant/RantNewPostPage')),
  chat: lazyPage(() => import('../features/chat/ChatPage')),
  profile: lazyPage(() => import('../features/profile/ProfilePage')),
  settings: lazyPage(() => import('../features/profile/SettingsPage')),
  account: lazyPage(() => import('../features/profile/AccountCenterPage')),
  security: lazyPage(() => import('../features/profile/SecurityCenterPage')),
  privacy: lazyPage(() => import('../features/profile/PrivacySettingsPage')),
  notifySettings: lazyPage(() => import('../features/profile/NotificationSettingsPage')),
  feedback: lazyPage(() => import('../features/profile/FeedbackPage')),
  notifications: lazyPage(() => import('../features/notifications/NotificationCenterPage')),
}
const OnboardingOverlay = pages.onboarding.Page
const MatchResultPage = pages.match.Page
const RantBoardPage = pages.rantBoard.Page
const RantSearchPage = pages.rantSearch.Page
const RantDetailPage = pages.rantDetail.Page
const RantNewPostPage = pages.rantNew.Page
const ChatPage = pages.chat.Page
const ProfilePage = pages.profile.Page
const SettingsPage = pages.settings.Page
const AccountCenterPage = pages.account.Page
const SecurityCenterPage = pages.security.Page
const PrivacySettingsPage = pages.privacy.Page
const NotificationSettingsPage = pages.notifySettings.Page
const FeedbackPage = pages.feedback.Page
const NotificationCenterPage = pages.notifications.Page

function App() {
  const navigate = useNavigate()
  const location = useLocation()
  const { user, authenticate, logout } = useAuth()
  const { totalUnread } = useChatRealtime()
  const [showMatchResult, setShowMatchResult] = useState(false)
  const [selectedMoodLabel, setSelectedMoodLabel] = useState<string>('')
  // 「存今日」那支還在飛的請求。滑卡頁要先等它落地再取卡，
  // 否則後端會拿上一則今日去配對。
  const [pendingEntry, setPendingEntry] = useState<Promise<unknown> | null>(null)
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

  // FCM token 會輪替，所以每次啟動都要重送一次，否則伺服器那份過期後推播會靜靜地不到。
  // 只有已經授權的人才會真的做事，不會跳任何視窗。
  useEffect(() => {
    if (!user) return
    void syncPushRegistration()
  }, [user])

  // 點推播通知進來時，如果帶了房號就直接開那間房
  useEffect(() => onPushOpened((roomId) => {
    navigate(roomId ? `/chat?room=${encodeURIComponent(roomId)}` : '/chat')
  }), [navigate])

  // 第一個畫面畫完之後，趁空檔把其他分頁的程式碼先抓好——等使用者真的去點
  // 底部導覽的時候已經在手上了，不會看到「載入中」。
  // 只在登入後做：沒登入的人只會看到 LoginPage，替他抓整個 App 是浪費流量。
  // 新手引導那一包只有還沒看過的人需要，看過的人不抓。
  const userId = user?.userId
  useEffect(() => {
    if (!userId) return
    const run = () => {
      const seenOnboarding = !!localStorage.getItem(onboardingKey(userId))
      for (const [name, page] of Object.entries(pages)) {
        if (name === 'onboarding' && seenOnboarding) continue
        page.preload()
      }
    }
    // 先等一秒多再說：第一個畫面自己要打的那幾支 API（今日、配對額度、未讀）
    // 這時候才剛送出去，不要讓十幾包程式碼跟它們搶連線。之後再挑主執行緒有空的時候抓。
    let idle: number | undefined
    const timer = window.setTimeout(() => {
      if ('requestIdleCallback' in window) idle = window.requestIdleCallback(run, { timeout: 3000 })
      else run()
    }, 1200)
    return () => {
      window.clearTimeout(timer)
      if (idle !== undefined) window.cancelIdleCallback(idle)
    }
  }, [userId])

  // 啟動即初始化社群登入：網頁版 Google popup 回到本站時，外掛建構子會自動把結果回傳給主視窗。
  useEffect(() => {
    void ensureSocialInit()
  }, [])

  const pathToPage: Record<string, PageKey> = {
    '/': 'today', '/rant': 'rant', '/chat': 'chat', '/profile': 'profile', '/settings': 'profile',
  }
  const activePage: PageKey = pathToPage[location.pathname]
    ?? (location.pathname.startsWith('/rant') ? 'rant'
      : location.pathname.startsWith('/account') ? 'profile'
      : location.pathname.startsWith('/security') ? 'profile'
      : location.pathname.startsWith('/settings') ? 'profile'
      : location.pathname.startsWith('/notifications') ? 'profile'
      : 'today')

  function setActivePage(page: PageKey) {
    const pageToPath: Record<PageKey, string> = {
      today: '/', rant: '/rant', chat: '/chat', profile: '/profile',
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

  // 發文頁與搜尋頁隱藏底部導覽列（全螢幕沉浸），貼文詳細頁照常顯示
  const isDetail = location.pathname === '/rant/new' || location.pathname === '/rant/search'

  return (
    <>
      <main className="app-shell">
        {/* 底部導覽在 Suspense 外面：某一頁的程式碼還在路上時，導覽列照樣在、照樣能按 */}
        <Suspense fallback={<RouteFallback />}>
        <Routes>
          <Route path="/" element={
            !showMatchResult
              ? <TodayPage onGoToMatches={(label, pending) => { setSelectedMoodLabel(label); setPendingEntry(pending); setShowMatchResult(true) }} />
              : <MatchResultPage moodLabel={selectedMoodLabel} pendingEntry={pendingEntry} onBack={() => setShowMatchResult(false)} onGoToRant={() => { setShowMatchResult(false); navigate('/rant') }} />
          } />
          <Route path="/rant" element={<RantBoardPage />} />
          <Route path="/rant/search" element={<RantSearchPage />} />
          <Route path="/rant/new" element={<RantNewPostPage />} />
          <Route path="/rant/:rantId" element={<RantDetailPage />} />
          {/* <Route path="/tasks" element={<TasksPage />} /> 任務功能暫時下架（後端保留） */}
          <Route path="/chat" element={<ChatPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/account" element={<AccountCenterPage />} />
          <Route path="/security" element={<SecurityCenterPage />} />
          <Route path="/settings/privacy" element={<PrivacySettingsPage />} />
          <Route path="/settings/notifications" element={<NotificationSettingsPage />} />
          <Route path="/settings/feedback" element={<FeedbackPage />} />
          <Route path="/notifications" element={<NotificationCenterPage />} />
        </Routes>
        </Suspense>
        {!isDetail && <BottomNav activePage={activePage} onChange={setActivePage} chatUnread={totalUnread} />}
      </main>
      {mode === 'night' && <GlobalNightEffects />}
      <ThemeTransitionOverlay />
      {!onboardingDone && (
        <Suspense fallback={null}>
          <OnboardingOverlay user={user} onComplete={handleOnboardingComplete} />
        </Suspense>
      )}
      {/* 以前用社群登入建的帳號沒問過條款與年齡，登入後補問一次。
          key 綁使用者：換一個帳號登入要重新問後端，不能沿用上一個人的答案。
          放在 onboarding 後面＝疊在它上面：先確認能不能用，再談怎麼用。 */}
      <ConsentGate key={user.userId} userId={user.userId} onLogout={logout} />
    </>
  )
}

export default App
