import { useState } from 'react'
import { Heart } from 'lucide-react'

type Props = {
  /** 伺服器說的：我按過了沒 */
  liked: boolean
  count: number
  /**
   * like = true 是按上去，false 是收回。
   * 回傳 Promise，失敗要 reject——這裡靠它決定要不要把愛心退回原樣。
   */
  onToggle: (like: boolean) => Promise<unknown> | void
  size?: number
  className?: string
  label?: string
}

// 六個小點往外噴的角度
const SPARKS = [0, 60, 120, 180, 240, 300]

/**
 * 樹洞的愛心（貼文和回應共用）。按一下是按上去，再按一下收回。
 *
 * 按下去當場就變、數字跟著動，不等伺服器回來。等伺服器的話，手指離開之後畫面要過
 * 幾百毫秒才有反應，那段空白會讓人再按一次——而現在再按一次是「收回」，
 * 等於自己把剛按的愛心取消掉。所以上一下還沒回來之前，這顆不收第二下。
 * 失敗就退回按之前的樣子。
 *
 * 按上去：彈一下、噴小點。收回：輕輕縮一下，不噴。動畫只動 transform 和 opacity。
 */
export default function HeartButton({ liked, count, onToggle, size = 16, className = '', label = '愛心' }: Props) {
  // 送出去、還沒等到伺服器的那個狀態；null = 沒有在等
  const [wish, setWish] = useState<boolean | null>(null)
  // 每按一次加一，拿來當 key 讓動畫重播；burst 記這一下是不是「按上去」
  const [tap, setTap] = useState(0)
  const [burst, setBurst] = useState(false)

  const on = wish ?? liked
  const shown = Math.max(0, count + (wish === null || wish === liked ? 0 : wish ? 1 : -1))

  async function handle() {
    if (wish !== null) return
    const next = !liked
    setTap((n) => n + 1)
    setBurst(next)
    setWish(next)
    try {
      await onToggle(next)
    } catch {
      // 失敗就讓它退回原樣；錯誤訊息不在這顆按鈕上講
    } finally {
      setWish(null)
    }
  }

  return (
    <button
      type="button"
      className={`heart-btn${on ? ' is-on' : ''} ${className}`.trim()}
      aria-pressed={on}
      aria-label={on ? `收回${label}` : label}
      onClick={handle}
    >
      <span key={tap} className={`heart-btn-icon${tap === 0 ? '' : burst ? ' is-pop' : ' is-nudge'}`}>
        <Heart size={size} fill={on ? 'currentColor' : 'none'} />
        {burst && tap > 0 && SPARKS.map((deg) => (
          <i key={deg} style={{ ['--deg' as string]: `${deg}deg` }} />
        ))}
      </span>
      {shown > 0 && <span className="heart-btn-count">{shown}</span>}
    </button>
  )
}
