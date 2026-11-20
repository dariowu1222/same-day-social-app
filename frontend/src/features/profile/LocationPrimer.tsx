import { useState } from 'react'
import { EyeOff, MapPin, ShieldCheck } from 'lucide-react'
import { locateMyArea, type LocateResult } from '../../shared/geo/locate'
import { locateFailureCopy } from './locationCopy'

/**
 * 定位說明頁：先講清楚要拿定位做什麼、不做什麼，使用者按了「開啟定位」
 * 才讓系統跳權限視窗。
 *
 * 為什麼要多這一頁、而不是一進來就讓系統直接問：Android 連續拒絕兩次之後
 * 系統就永遠不再跳視窗，iOS 更是只有一次機會。在使用者還不知道定位要幹嘛的
 * 時候問，被按掉的機率最高，而且按掉就救不回來。這一頁上的「先不用」不會動到
 * 系統權限，所以選了它的人之後還有完整的機會。
 *
 * 也因為這樣，這一頁絕對不能自動觸發定位——一定要等使用者自己按。
 */

type Props = {
  /** 離開這一頁。area 是定位帶出來的縣市；沒開定位或沒定到就是 null。 */
  onDone: (area: string | null) => void
}

type Stage =
  | { kind: 'ask' }
  | { kind: 'locating' }
  | { kind: 'result'; result: LocateResult }

const PROMISES = [
  { icon: MapPin, text: '只用來判斷你在哪個縣市' },
  { icon: ShieldCheck, text: '座標只在手機上換算，不上傳、不儲存' },
  { icon: EyeOff, text: '別人只看得到縣市，看不到你的位置' },
] as const

function LocationVisual({ pulsing, found }: { pulsing: boolean; found: boolean }) {
  return (
    <div className={`ob-location-visual${pulsing ? ' pulsing' : ''}${found ? ' found' : ''}`} aria-hidden="true">
      <svg viewBox="0 0 200 200" width="100%" height="100%">
        <defs>
          <linearGradient id="ob-location-pin" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stopColor="#f39c7c" />
            <stop offset="1" stopColor="#d86c4e" />
          </linearGradient>
        </defs>

        {/* 街道 */}
        <path
          d="M-10 96 L210 70 M-10 168 L210 150 M58 -10 L78 210 M146 -10 L170 210"
          stroke="#d3eae3"
          strokeWidth="11"
          strokeLinecap="round"
          fill="none"
        />

        {/* 以大頭針落點為中心的範圍圈（壓扁成橢圓，看起來是躺在地上的） */}
        <ellipse className="ob-location-ring ring-3" cx="100" cy="136" rx="86" ry="33" />
        <ellipse className="ob-location-ring ring-2" cx="100" cy="136" rx="58" ry="22" />
        <ellipse className="ob-location-ring ring-1" cx="100" cy="136" rx="30" ry="11.5" />

        {/* 附近的人 */}
        <circle cx="44" cy="126" r="8" fill="#f6c453" stroke="#fffdf8" strokeWidth="3" />
        <circle cx="152" cy="150" r="8" fill="#7fbfb3" stroke="#fffdf8" strokeWidth="3" />
        <circle cx="132" cy="112" r="6.5" fill="#f0a78f" stroke="#fffdf8" strokeWidth="3" />

        {/* 大頭針 */}
        <ellipse cx="100" cy="137" rx="11" ry="4" fill="#3e1d0a" opacity="0.16" />
        <path
          d="M100 44c-18.8 0-34 15-34 33.6 0 24.6 34 58.4 34 58.4s34-33.8 34-58.4C134 59 118.8 44 100 44z"
          fill="url(#ob-location-pin)"
        />
        {found ? (
          <>
            <circle cx="100" cy="78" r="15" fill="#fffdf8" />
            <path d="M92.5 78.5l5.5 5.5 10-11" stroke="#4e9188" strokeWidth="3.6" strokeLinecap="round" strokeLinejoin="round" fill="none" />
          </>
        ) : (
          <circle cx="100" cy="78" r="12.5" fill="#fffdf8" />
        )}
      </svg>
    </div>
  )
}

export default function LocationPrimer({ onDone }: Props) {
  const [stage, setStage] = useState<Stage>({ kind: 'ask' })

  // 定位最久會等十幾秒，使用者可能等不及先按了「先不用」。那時這個元件已經被
  // 換掉，晚到的結果 setStage 不會有任何效果，也不會被帶進所在縣市——
  // 他選了不用，就是不用。
  async function locate() {
    if (stage.kind === 'locating') return
    setStage({ kind: 'locating' })
    setStage({ kind: 'result', result: await locateMyArea() })
  }

  const leave = onDone

  const result = stage.kind === 'result' ? stage.result : null
  const found = result?.status === 'ok' ? result.area : null
  const failure = result && result.status !== 'ok' ? result : null
  const failureCopy = failure ? locateFailureCopy(failure, 'onboarding') : null
  // 沒開定位服務、暫時抓不到：再按一次有機會成功，所以「再試一次」當主要按鈕。
  // 被拒絕、人在國外：再按也一樣（或只會更惹人煩），主要按鈕是往下走。
  const retryFirst = failure?.status === 'off' || failure?.status === 'unavailable'

  return (
    <div className="ob-location">
      <div className="ob-location-body">
        <LocationVisual pulsing={stage.kind === 'locating'} found={found !== null} />

        {/* 狀態變了要讓螢幕閱讀器念出來：定位中 → 結果 */}
        <div className="ob-location-text" role="status" aria-live="polite">
          {found ? (
            <>
              <h2 className="ob-slide-title">你在{found}</h2>
              <p className="ob-slide-desc">已經幫你帶入所在縣市<br />不對的話，稍後可以自己改</p>
            </>
          ) : failureCopy ? (
            <>
              <h2 className="ob-slide-title">{failureCopy.title}</h2>
              <p className="ob-slide-desc">{failureCopy.detail}</p>
            </>
          ) : (
            <>
              <h2 className="ob-slide-title">讓同頻的人，離你近一點</h2>
              <p className="ob-slide-desc">開啟定位，幫你帶入所在的縣市<br />推薦距離合適的人</p>
            </>
          )}
        </div>

        {!result && (
          <ul className="ob-location-promises">
            {PROMISES.map(({ icon: Icon, text }) => (
              <li key={text}>
                <span className="ob-location-promise-icon" aria-hidden="true">
                  <Icon size={16} strokeWidth={2.2} />
                </span>
                {text}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="ob-setup-footer">
        {found ? (
          <button className="ob-primary-btn" type="button" onClick={() => leave(found)}>
            繼續 →
          </button>
        ) : failure ? (
          <>
            <button
              className="ob-primary-btn"
              type="button"
              onClick={retryFirst ? locate : () => leave(null)}
            >
              {retryFirst ? '再試一次' : '繼續 →'}
            </button>
            {retryFirst ? (
              <button className="ob-text-btn" type="button" onClick={() => leave(null)}>
                先繼續，稍後自己選縣市
              </button>
            ) : failure.status === 'denied' && (
              <button className="ob-text-btn" type="button" onClick={locate}>
                再試一次
              </button>
            )}
          </>
        ) : (
          <>
            <button
              className="ob-primary-btn"
              type="button"
              disabled={stage.kind === 'locating'}
              onClick={locate}
            >
              {stage.kind === 'locating' ? '定位中⋯' : '開啟定位'}
            </button>
            <button className="ob-text-btn" type="button" onClick={() => leave(null)}>
              先不用，稍後自己選縣市
            </button>
          </>
        )}
      </div>
    </div>
  )
}
