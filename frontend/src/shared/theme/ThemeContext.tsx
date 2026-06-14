import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'

export type ThemePreference = 'day' | 'night' | 'auto'
export type ThemeMode = 'day' | 'night'

type ThemeCtx = {
  preference: ThemePreference
  mode: ThemeMode
  isAnimating: boolean
  pendingMode: ThemeMode | null
  setPreference: (pref: ThemePreference) => void
  commitMode: () => void
  finishAnimation: () => void
}

const Ctx = createContext<ThemeCtx | null>(null)

export function useTheme() {
  const ctx = useContext(Ctx)
  if (!ctx) throw new Error('useTheme outside ThemeProvider')
  return ctx
}

const PREF_KEY = 'same-day-theme-pref'

function resolveMode(pref: ThemePreference, systemDark: boolean): ThemeMode {
  if (pref === 'day') return 'day'
  if (pref === 'night') return 'night'
  return systemDark ? 'night' : 'day'
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(
    () => (localStorage.getItem(PREF_KEY) as ThemePreference | null) ?? 'auto'
  )
  const [systemDark, setSystemDark] = useState(
    () => window.matchMedia('(prefers-color-scheme: dark)').matches
  )
  const [mode, setMode] = useState<ThemeMode>(
    () => resolveMode((localStorage.getItem(PREF_KEY) as ThemePreference | null) ?? 'auto',
      window.matchMedia('(prefers-color-scheme: dark)').matches)
  )
  const [isAnimating, setIsAnimating] = useState(false)
  const [pendingMode, setPendingMode] = useState<ThemeMode | null>(null)
  const [pendingPref, setPendingPref] = useState<ThemePreference | null>(null)

  // 監聽系統深色模式變化（僅 auto 時生效）
  useEffect(() => {
    const mq = window.matchMedia('(prefers-color-scheme: dark)')
    const handler = (e: MediaQueryListEvent) => {
      setSystemDark(e.matches)
      // auto 模式下跟隨系統變化（直接切換，不觸發動畫）
      setPreferenceState(prev => {
        if (prev === 'auto') setMode(e.matches ? 'night' : 'day')
        return prev
      })
    }
    mq.addEventListener('change', handler)
    return () => mq.removeEventListener('change', handler)
  }, [])

  const setPreference = useCallback((pref: ThemePreference) => {
    if (isAnimating) return
    const nextMode = resolveMode(pref, systemDark)
    if (nextMode === mode) {
      // 模式不變，直接更新偏好（不觸發動畫）
      setPreferenceState(pref)
      localStorage.setItem(PREF_KEY, pref)
    } else {
      // 模式會切換，觸發過場動畫
      setPendingPref(pref)
      setPendingMode(nextMode)
      setIsAnimating(true)
    }
  }, [isAnimating, mode, systemDark])

  const commitMode = useCallback(() => {
    if (pendingMode !== null) {
      setMode(pendingMode)
    }
    if (pendingPref !== null) {
      setPreferenceState(pendingPref)
      localStorage.setItem(PREF_KEY, pendingPref)
    }
  }, [pendingMode, pendingPref])

  const finishAnimation = useCallback(() => {
    setPendingMode(null)
    setPendingPref(null)
    setIsAnimating(false)
  }, [])

  return (
    <Ctx.Provider value={{ preference, mode, isAnimating, pendingMode, setPreference, commitMode, finishAnimation }}>
      {children}
    </Ctx.Provider>
  )
}
