import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { flushSync } from 'react-dom'

export type ThemePreference = 'day' | 'night' | 'auto'
export type ThemeMode = 'day' | 'night'

/** 這次切換演的是哪一段過場；null 表示沒有演（模式沒變、關掉動態效果、或走舊過場）。 */
export type ThemeSpread = 'to-night' | 'to-day' | null

type ThemeCtx = {
  preference: ThemePreference
  mode: ThemeMode
  isAnimating: boolean
  pendingMode: ThemeMode | null
  /**
   * 剛切過去、過場還在收尾的那幾秒：'night' 時螢火蟲一隻隻亮起來，'day' 時有一顆太陽升起。
   * 平常是 null。
   */
  arriving: ThemeMode | null
  /** 回傳這次演的是入夜還是清晨——按鈕自己要配合動一下的話靠這個決定時機。 */
  setPreference: (pref: ThemePreference) => ThemeSpread
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
    () => (localStorage.getItem(PREF_KEY) as ThemePreference | null) ?? 'day'
  )
  const [systemDark, setSystemDark] = useState(
    () => window.matchMedia('(prefers-color-scheme: dark)').matches
  )
  const [mode, setMode] = useState<ThemeMode>(
    () => resolveMode((localStorage.getItem(PREF_KEY) as ThemePreference | null) ?? 'day',
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

  // 過場進行中。用 ref 不用 state：設成 state 會讓設定頁的按鈕先變成停用的淡色，
  // 那一格剛好被拍進「舊畫面」的快照裡，過場一開始就閃一下。
  const spreadingRef = useRef(false)
  const [arriving, setArriving] = useState<ThemeMode | null>(null)
  const arrivingTimer = useRef(0)
  useEffect(() => () => window.clearTimeout(arrivingTimer.current), [])

  const setPreference = useCallback((pref: ThemePreference): ThemeSpread => {
    if (isAnimating || spreadingRef.current) return null
    const nextMode = resolveMode(pref, systemDark)
    const apply = () => {
      setMode(nextMode)
      setPreferenceState(pref)
      localStorage.setItem(PREF_KEY, pref)
    }
    if (nextMode === mode) {
      // 模式不變，直接更新偏好（不觸發動畫）
      apply()
      return null
    }
    // 關掉動態效果的人：直接換，不演
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      apply()
      return null
    }
    if (typeof document.startViewTransition !== 'function') {
      // 舊的 WebView（Chrome 111 以前）沒有 View Transitions：退回整片蓋上去的舊過場
      setPendingPref(pref)
      setPendingMode(nextMode)
      setIsAnimating(true)
      return null
    }

    // 入夜：夜色從天上降下來。清晨：光從地平線往上漫開。
    // 瀏覽器先拍下舊畫面，我們在 callback 裡把主題換掉，它再拍新畫面，
    // 兩張之間用一道邊緣很柔的遮罩過場（樣式在 09-theme-night.css）。
    // 露出來的是「真的新畫面」，不是一片純色——過場中途內容一直看得到、讀得到。
    const rootEl = document.documentElement
    // 清晨的光是以畫面底邊中點為圓心的圓：半徑要蓋得到最遠的上面兩個角
    const r = Math.hypot(window.innerWidth / 2, window.innerHeight)
    rootEl.style.setProperty('--theme-r', `${Math.ceil(r)}px`)
    const spread: ThemeSpread = nextMode === 'night' ? 'to-night' : 'to-day'
    rootEl.dataset.themeSwitch = spread
    spreadingRef.current = true

    const transition = document.startViewTransition(() => {
      flushSync(() => {
        apply()
        // 螢火蟲／太陽在這裡才出現，所以只會在新畫面裡，不會被拍進舊畫面
        setArriving(nextMode)
      })
      // data-theme 平常是 App.tsx 的 effect 在設；這裡自己先設一次，
      // 確保瀏覽器拍新畫面的時候主題已經換好，不必賭 effect 跑得夠不夠早。
      rootEl.setAttribute('data-theme', nextMode === 'night' ? 'dark' : '')
    })
    // 螢火蟲全部亮完大約 3.4 秒、太陽 2 秒就淡出；到時間把收尾的狀態拿掉
    window.clearTimeout(arrivingTimer.current)
    arrivingTimer.current = window.setTimeout(() => setArriving(null), nextMode === 'night' ? 3800 : 2300)
    const cleanup = () => {
      delete rootEl.dataset.themeSwitch
      rootEl.style.removeProperty('--theme-r')
      spreadingRef.current = false
    }
    transition.finished.then(cleanup, cleanup)
    return spread
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
    <Ctx.Provider value={{ preference, mode, isAnimating, pendingMode, arriving, setPreference, commitMode, finishAnimation }}>
      {children}
    </Ctx.Provider>
  )
}
