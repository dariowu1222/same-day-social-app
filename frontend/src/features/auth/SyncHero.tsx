import { useLayoutEffect, useRef, type CSSProperties } from 'react'

// 合成愛心那一下往外噴的小點
const SPARKS = [20, 80, 140, 200, 260, 320]

/**
 * 歡迎頁的主視覺：兩顆圓，靠在一起就是一顆愛心。
 *
 * 珊瑚那顆是我，綠的是對方；平常各自浮著、互相看著。merged 變成 true 的時候
 * 兩顆滑到一起、綠的那顆變成同一個顏色、底下長出一個菱形——兩個圓加一個
 * 轉 45 度的方塊，正好是一顆愛心的形狀，不必另外畫一張圖。
 *
 * 全部只動 transform 和 opacity（樣式在 15-welcome.css）。
 */
export default function SyncHero({ merged }: { merged: boolean }) {
  const root = useRef<HTMLDivElement>(null)

  // 兩顆圓平常各自上下浮動，而且刻意錯開半拍。要合成愛心時必須先回到同一條線上，
  // 不然兩個半邊一高一低，愛心是歪的。
  //
  // 直接把浮動的動畫拿掉會「跳」一下回原位。所以這裡先讀出它此刻飄到哪裡、
  // 把那個位置寫死，再用 0.3 秒滑回正中——跟兩顆圓往中間靠是同時發生的，看不出接縫。
  // 用 useLayoutEffect：要趕在這一格畫出來之前把位置接住。
  useLayoutEffect(() => {
    const bobs = root.current?.querySelectorAll<HTMLElement>('.sync-bob') ?? []
    bobs.forEach((el) => {
      if (!merged) {
        el.style.cssText = ''
        return
      }
      const now = getComputedStyle(el).transform
      el.style.animation = 'none'
      el.style.transform = now
      void el.offsetWidth
      el.style.transition = 'transform 0.3s'
      el.style.transform = 'none'
    })
  }, [merged])

  const ball = (
    <span className="sync-bob">
      <span className="sync-ball"><i /><i /></span>
    </span>
  )

  return (
    <div ref={root} className={`sync-hero${merged ? ' is-merged' : ''}`} aria-hidden="true">
      <span className="sync-diamond" />
      <span className="sync-dot sync-dot-me">{ball}</span>
      <span className="sync-dot sync-dot-you">{ball}</span>
      {SPARKS.map((deg) => (
        <span key={deg} className="sync-spark" style={{ ['--deg' as string]: `${deg}deg` } as CSSProperties} />
      ))}
    </div>
  )
}
