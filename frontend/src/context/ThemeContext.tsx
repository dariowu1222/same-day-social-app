import { createContext, useCallback, useContext, useState } from 'react'
import type { ReactNode } from 'react'

export type ThemeMode = 'day' | 'night'

type ThemeCtx = {
  mode: ThemeMode
  isAnimating: boolean
  pendingMode: ThemeMode | null
  startToggle: () => void
  commitMode: () => void
  finishAnimation: () => void
}

const Ctx = createContext<ThemeCtx | null>(null)

export function useTheme() {
  const ctx = useContext(Ctx)
  if (!ctx) throw new Error('useTheme outside ThemeProvider')
  return ctx
}

const KEY = 'same-day-theme'

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [mode, setMode] = useState<ThemeMode>(
    () => (localStorage.getItem(KEY) as ThemeMode | null) ?? 'day'
  )
  const [isAnimating, setIsAnimating] = useState(false)
  const [pendingMode, setPendingMode] = useState<ThemeMode | null>(null)

  const startToggle = useCallback(() => {
    if (isAnimating) return
    const next: ThemeMode = mode === 'day' ? 'night' : 'day'
    setPendingMode(next)
    setIsAnimating(true)
  }, [mode, isAnimating])

  const commitMode = useCallback(() => {
    if (pendingMode !== null) {
      setMode(pendingMode)
      localStorage.setItem(KEY, pendingMode)
    }
  }, [pendingMode])

  const finishAnimation = useCallback(() => {
    setPendingMode(null)
    setIsAnimating(false)
  }, [])

  return (
    <Ctx.Provider value={{ mode, isAnimating, pendingMode, startToggle, commitMode, finishAnimation }}>
      {children}
    </Ctx.Provider>
  )
}
