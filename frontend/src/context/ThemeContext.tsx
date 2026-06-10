import { createContext, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'

export type ThemePreference = 'day' | 'night' | 'auto'
export type ThemeMode = 'day' | 'night'

type ThemeCtx = {
  preference: ThemePreference
  mode: ThemeMode
  setPreference: (pref: ThemePreference) => void
}

const Ctx = createContext<ThemeCtx | null>(null)

export function useTheme() {
  const ctx = useContext(Ctx)
  if (!ctx) throw new Error('useTheme outside ThemeProvider')
  return ctx
}

const PREF_KEY = 'same-day-theme-pref'

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(
    () => (localStorage.getItem(PREF_KEY) as ThemePreference | null) ?? 'auto'
  )
  const [systemDark, setSystemDark] = useState(
    () => window.matchMedia('(prefers-color-scheme: dark)').matches
  )

  useEffect(() => {
    const mq = window.matchMedia('(prefers-color-scheme: dark)')
    const handler = (e: MediaQueryListEvent) => setSystemDark(e.matches)
    mq.addEventListener('change', handler)
    return () => mq.removeEventListener('change', handler)
  }, [])

  const mode: ThemeMode =
    preference === 'day' ? 'day' :
    preference === 'night' ? 'night' :
    systemDark ? 'night' : 'day'

  function setPreference(pref: ThemePreference) {
    setPreferenceState(pref)
    localStorage.setItem(PREF_KEY, pref)
  }

  return (
    <Ctx.Provider value={{ preference, mode, setPreference }}>
      {children}
    </Ctx.Provider>
  )
}
