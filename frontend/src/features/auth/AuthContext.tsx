import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { STORAGE_KEYS, rememberSet, rememberGet, rememberClear } from '../../shared/lib/storageKeys'
import { clearAuthToken } from '../../shared/api/httpClient'
import type { DemoUser } from './types'

type AuthContextValue = {
  user: DemoUser | null
  // 直接覆寫使用者（例如 Profile 改暱稱後同步）。會一併寫回 storage。
  setUser: (user: DemoUser | null) => void
  // 登入成功後呼叫：寫入 storage + 設定狀態。
  authenticate: (user: DemoUser, remember: boolean) => void
  // 登出：清 token + 使用者，回到登入頁。
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

// 是否「記住我」由現有 storage 落點推斷：localStorage = 記住。
function persistUser(user: DemoUser | null, remember: boolean) {
  if (!user) {
    rememberClear(STORAGE_KEYS.user)
    return
  }
  rememberSet(STORAGE_KEYS.user, JSON.stringify(user), remember)
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUserState] = useState<DemoUser | null>(null)

  useEffect(() => {
    const saved = rememberGet(STORAGE_KEYS.user)
    if (saved) {
      try {
        // 掛載時從 storage 還原登入狀態；此初始化 setState 為刻意行為。
        // eslint-disable-next-line react-hooks/set-state-in-effect
        setUserState(JSON.parse(saved) as DemoUser)
      } catch {
        rememberClear(STORAGE_KEYS.user)
      }
    }
  }, [])

  function authenticate(nextUser: DemoUser, remember: boolean) {
    persistUser(nextUser, remember)
    setUserState(nextUser)
  }

  function setUser(nextUser: DemoUser | null) {
    // 沿用既有落點：使用者已在 localStorage 就維持記住，否則 session。
    const remember = localStorage.getItem(STORAGE_KEYS.user) != null
    persistUser(nextUser, remember)
    setUserState(nextUser)
  }

  function logout() {
    clearAuthToken()
    rememberClear(STORAGE_KEYS.user)
    setUserState(null)
  }

  return (
    <AuthContext.Provider value={{ user, setUser, authenticate, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth 必須在 AuthProvider 內使用')
  return ctx
}
