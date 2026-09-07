import { useEffect, useState } from 'react'

/**
 * 桌面版斷點。
 *
 * 必須與 styles/17-desktop.css 的 @media (min-width: 1024px) 一致——
 * 兩邊不同步會出現「版面已切成桌面、但元件還照手機渲染」這種很難查的錯位。
 * 要改斷點請同時改這兩處。
 */
export const DESKTOP_MIN_WIDTH = 1024
const QUERY = `(min-width: ${DESKTOP_MIN_WIDTH}px)`

/**
 * 目前是否為桌面版面。
 *
 * 用途是讓元件「只在桌面渲染某些內容」（例如側邊導覽的品牌區與使用者區），
 * 手機版連 DOM 都不會產生，因此不可能影響既有的手機樣式與行為。
 */
export function useIsDesktop(): boolean {
  const [isDesktop, setIsDesktop] = useState(
    () => typeof window !== 'undefined' && window.matchMedia(QUERY).matches,
  )

  useEffect(() => {
    const mq = window.matchMedia(QUERY)
    const onChange = (e: MediaQueryListEvent) => setIsDesktop(e.matches)
    // 掛載當下先同步一次：初始值與實際狀態可能因視窗在掛載前被調整而不同
    setIsDesktop(mq.matches)
    mq.addEventListener('change', onChange)
    return () => mq.removeEventListener('change', onChange)
  }, [])

  return isDesktop
}
