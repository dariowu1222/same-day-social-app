import { useEffect, useRef, useState } from 'react'
import { Pause, Play } from 'lucide-react'

/**
 * 錄好、還沒送出的語音草稿的試聽器。
 *
 * 不用原生的 <audio controls>：草稿還是 data URL，而 MediaRecorder 產出的檔案
 * 標頭裡沒有長度（它邊錄邊寫，錄完才知道），瀏覽器讀到的 duration 是 Infinity，
 * 原生進度條算不出比例，一播放就頂到底。已送出的那幾則是從 Supabase 用 HTTP
 * 取回的，可以 range request、掃得到結尾，所以只有草稿這一段會壞。
 *
 * 長度改由錄音那端量好傳進來（useAudioRecorder），這裡就不必問瀏覽器。
 * 播放時的 currentTime 本身是準的——Infinity 只影響 duration 與 seek。
 *
 * 順帶解決的事：原生控制列的高度與內距我們管不到，旁邊的 X 和「傳送」
 * 怎麼對都會差幾個 pixel。自己畫就沒有這個問題。
 */
type Props = {
  src: string
  durationMs: number
}

function clock(seconds: number) {
  const whole = Math.max(0, Math.floor(seconds))
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, '0')}`
}

export default function VoiceDraftPlayer({ src, durationMs }: Props) {
  const audioRef = useRef<HTMLAudioElement>(null)
  const [playing, setPlaying] = useState(false)
  const [at, setAt] = useState(0)

  const total = Math.max(durationMs / 1000, 0.1)

  // 換一段錄音就從頭開始，不要沿用上一段的進度
  useEffect(() => {
    setPlaying(false)
    setAt(0)
  }, [src])

  function toggle() {
    const el = audioRef.current
    if (!el) return
    if (playing) {
      el.pause()
      setPlaying(false)
      return
    }
    // 播完之後再按一次要從頭播
    if (at >= total) {
      el.currentTime = 0
      setAt(0)
    }
    void el.play()
    setPlaying(true)
  }

  // 實際長度可能跟量到的差幾十毫秒，夾住避免進度條超出軌道
  const progress = Math.min(at / total, 1)

  return (
    <div className="cr-voice-player">
      <audio
        ref={audioRef}
        src={src}
        preload="auto"
        onTimeUpdate={(e) => setAt(e.currentTarget.currentTime)}
        onEnded={() => { setPlaying(false); setAt(0) }}
      />
      <button
        className="cr-voice-player-btn"
        type="button"
        onClick={toggle}
        aria-label={playing ? '暫停試聽' : '試聽錄音'}
      >
        {playing ? <Pause size={14} fill="currentColor" /> : <Play size={14} fill="currentColor" />}
      </button>
      <div className="cr-voice-player-track">
        <div className="cr-voice-player-fill" style={{ width: `${progress * 100}%` }} />
      </div>
      {/* 寫成「經過 / 總長」，跟已送出訊息那一則的原生控制列同一個樣子 */}
      <span className="cr-voice-player-time">
        {clock(at)} / {clock(total)}
      </span>
    </div>
  )
}
