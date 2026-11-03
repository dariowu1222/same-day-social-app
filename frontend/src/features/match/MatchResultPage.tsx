import { useEffect, useRef, useState, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { SlidersHorizontal, X } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { getTodayMatches, likeMatch, passMatch } from './api'
import {
  rememberPassed, readPassedIds,
  rememberPendingLike, readPendingLikes, clearPendingLike,
} from './decidedCache'
import { ApiError } from '../../shared/api/httpClient'
import { getProfile, updateProfile } from '../profile/api'
import { getZodiac, getAge } from '../profile/profileUtils'
import type { MatchResult } from './types'
import AgeRangeSlider, { AGE_FLOOR, AGE_CEIL } from '../../shared/ui/AgeRangeSlider'

type EnrichedMatch = Omit<MatchResult, 'photos' | 'interests' | 'personalityTags' | 'appearanceTags'> & {
  photos: string[]
  interests: string[]
  // 後端可能回 null，這裡統一收斂成陣列，渲染端就不用一路做 null 判斷
  personalityTags: string[]
  appearanceTags: string[]
  age?: number
  city?: string
  bio?: string
  zodiac?: string
}

/**
 * 右滑送不出去，是「等一下再試」還是「不用再試了」。
 *
 * 兩者的補救方式相反：暫時失敗要排進佇列下次補送，而對方已經不在的話，
 * 補送幾次結果都一樣，只是每次進頁面都白跑一趟。後端用 MATCH_UNAVAILABLE
 * 把這件事說清楚，不然這裡只能一視同仁當成暫時失敗。
 */
function isCandidateGone(err: unknown) {
  return err instanceof ApiError && err.code === 'MATCH_UNAVAILABLE'
}

const TUTORIAL_KEY = 'match-swipe-tutorial-done'
const LOOKING_FOR_OPTIONS = ['男生', '女生', '不限'] as const

const ZODIAC_ICON: Record<string, string> = {
  '牡羊座': '♈', '金牛座': '♉', '雙子座': '♊', '巨蟹座': '♋',
  '獅子座': '♌', '處女座': '♍', '天秤座': '♎', '天蠍座': '♏',
  '射手座': '♐', '摩羯座': '♑', '水瓶座': '♒', '雙魚座': '♓',
}

// 真實使用者資料稀疏時的優雅退場：無照片給穩定的佔位圖。
function placeholderPhotos(id: string): string[] {
  return [`https://picsum.photos/seed/${encodeURIComponent(id)}/400/500`]
}

function mapMatch(m: MatchResult): EnrichedMatch {
  const photos = (m.photos && m.photos.length > 0) ? m.photos : placeholderPhotos(m.matchedUserId)
  const age = m.age ?? (m.birthday ? getAge(m.birthday) : undefined)
  return {
    ...m,
    photos,
    interests: m.interests ?? [],
    personalityTags: m.personalityTags ?? [],
    appearanceTags: m.appearanceTags ?? [],
    age: age ?? undefined,
    city: m.city ?? undefined,
    bio: m.bio ?? undefined,
    zodiac: m.birthday ? getZodiac(m.birthday) : undefined,
  }
}

type Props = {
  onBack: () => void
  onGoToRant: () => void
  moodLabel?: string
  /**
   * 剛從今日頁送出、還在飛的「存今日」請求。
   * 取卡前要先等它落地——後端配對讀的是使用者最新一筆今日，
   * 沒等就會拿上一則（或完全沒有）去算分。
   */
  pendingEntry?: Promise<unknown> | null
}

type SwipeDir = 'left' | 'right' | null

function SwipeCard({
  match,
  isTop,
  stackOffset,
  onSwipe,
}: {
  match: EnrichedMatch
  isTop: boolean
  stackOffset: number
  onSwipe: (dir: SwipeDir) => void
}) {
  const cardRef = useRef<HTMLDivElement>(null)
  const startX = useRef(0)
  const startY = useRef(0)
  const currentX = useRef(0)
  const [dragX, setDragX] = useState(0)
  const [dragging, setDragging] = useState(false)
  const [flyDir, setFlyDir] = useState<SwipeDir>(null)
  const [photoIndex, setPhotoIndex] = useState(0)
  const [showProfile, setShowProfile] = useState(false)

  const THRESHOLD = 80
  const TAP_THRESHOLD = 8

  function onPointerDown(e: React.PointerEvent) {
    if (!isTop || showProfile) return
    startX.current = e.clientX
    startY.current = e.clientY
    currentX.current = 0
    setDragging(true)
    cardRef.current?.setPointerCapture(e.pointerId)
  }

  function onPointerMove(e: React.PointerEvent) {
    if (!dragging || showProfile) return
    const dx = e.clientX - startX.current
    currentX.current = dx
    setDragX(dx)
  }

  function onPointerUp(e: React.PointerEvent) {
    if (!dragging) return
    setDragging(false)
    const dx = currentX.current
    const dy = e.clientY - startY.current

    if (Math.abs(dx) < TAP_THRESHOLD && Math.abs(dy) < TAP_THRESHOLD) {
      const card = cardRef.current
      if (card) {
        const rect = card.getBoundingClientRect()
        // 量照片區的實際下緣，不要用寬度推估。
        // 原本寫 rect.width * 1.05，但照片高度是卡片高度的百分比
        // （收合 50%、展開 24%），跟寬度無關：收合時約差 36px、展開時
        // 差到 200px，資訊區頂端那一帶會被誤判成照片而變成翻照片。
        const photo = card.querySelector('.swipe-photo-wrap')
        const photoBottom = photo
          ? photo.getBoundingClientRect().bottom
          : rect.top + rect.width * 1.05
        if (e.clientY < photoBottom) {
          if (match.photos.length > 1) {
            const relX = e.clientX - rect.left
            if (relX < rect.width / 2) {
              setPhotoIndex(i => Math.max(0, i - 1))
            } else {
              setPhotoIndex(i => Math.min(match.photos.length - 1, i + 1))
            }
          }
          setDragX(0)
          return
        }
        if (!showProfile) {
          setShowProfile(true)
          setDragX(0)
          return
        }
      }
    }

    if (Math.abs(dx) >= THRESHOLD) {
      const dir: SwipeDir = dx > 0 ? 'right' : 'left'
      setFlyDir(dir)
      setTimeout(() => onSwipe(dir), 320)
    } else {
      setDragX(0)
    }
  }

  const rotate = dragX * 0.08
  const likeOpacity = Math.min(1, Math.max(0, dragX / 80))
  const nopeOpacity = Math.min(1, Math.max(0, -dragX / 80))

  const flyStyle = flyDir
    ? { transform: `translateX(${flyDir === 'right' ? 140 : -140}%) rotate(${flyDir === 'right' ? 24 : -24}deg)`, transition: 'transform 0.32s ease-in', opacity: 0 }
    : dragging
      ? { transform: `translateX(${dragX}px) rotate(${rotate}deg)`, transition: 'none' }
      : { transform: `translateX(${dragX}px) rotate(${rotate}deg) scale(${1 - stackOffset * 0.05}) translateY(${stackOffset * 14}px)`, transition: 'transform 0.3s ease' }

  const baseStyle: React.CSSProperties = {
    position: 'absolute',
    inset: 0,
    zIndex: 10 - stackOffset,
    cursor: isTop && !showProfile ? 'grab' : 'default',
    userSelect: 'none',
    touchAction: showProfile ? 'pan-y' : 'none',
    ...flyStyle,
  }

  if (!isTop && stackOffset > 0) {
    baseStyle.transform = `scale(${1 - stackOffset * 0.05}) translateY(${stackOffset * 14}px)`
    baseStyle.transition = 'transform 0.3s ease'
    baseStyle.filter = `brightness(${1 - stackOffset * 0.06})`
  }

  return (
    <div
      ref={cardRef}
      className="swipe-card"
      style={baseStyle}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={(e) => onPointerUp(e)}
    >
      <div className="swipe-label swipe-label-like" style={{ opacity: likeOpacity }}>同頻 ✦</div>
      <div className="swipe-label swipe-label-nope" style={{ opacity: nopeOpacity }}>跳過 ✕</div>

      <div className={`swipe-photo-wrap${showProfile ? ' compressed' : ''}`}>
        <img
          key={photoIndex}
          src={match.photos[photoIndex]}
          alt={match.nickname}
          className="swipe-photo"
          draggable={false}
        />
        {match.photos.length > 1 && (
          <>
            <div className="swipe-photo-hint left" />
            <div className="swipe-photo-hint right" />
            <div className="swipe-photo-dots">
              {match.photos.map((_, i) => (
                <span key={i} className={`swipe-photo-dot${i === photoIndex ? ' active' : ''}`} />
              ))}
            </div>
          </>
        )}
      </div>

      <div
        className={`swipe-card-info${showProfile ? ' expanded' : ''}`}
        onClick={() => isTop && !showProfile && setShowProfile(true)}
      >
        <div className="swipe-name-row">
          <h2 className="swipe-name">{match.nickname}</h2>
          {match.age != null && <span className="swipe-age">{match.age}</span>}
          {match.city && <span className="swipe-city">{match.city}</span>}
          {match.zodiac && (
            <span className="swipe-zodiac">
              <span className="swipe-zodiac-icon">{ZODIAC_ICON[match.zodiac] ?? '✦'}</span>
              {match.zodiac}
            </span>
          )}
        </div>
        {match.bio && <p className="swipe-bio">{match.bio}</p>}

        <div className="swipe-divider" />

        <div className="swipe-resonance-strip">
          <span className="swipe-resonance-dot" />
          <span>{match.reason}</span>
        </div>

        {/* 沒發過今日的人也會進牌堆，這時 todaySummary 是空字串——
            整塊要收掉，不然卡片上會留一行標題加一對空引號「」。 */}
        {match.todaySummary.trim() && (
          <>
            <p className="swipe-today-label">今天想說的話</p>
            <p className={`swipe-today-text${showProfile ? '' : ' clamped'}`}>
              「{match.todaySummary}」
            </p>
          </>
        )}

        {match.interests.length > 0 && (
          <div className="swipe-tags">
            {match.interests.slice(0, showProfile ? undefined : 3).map(tag => (
              <span key={tag} className="swipe-tag">{tag}</span>
            ))}
            {!showProfile && match.interests.length > 3 && (
              <span className="swipe-tag swipe-tag-more">+{match.interests.length - 3}</span>
            )}
          </div>
        )}

        <p className="swipe-score-row">
          <span className="swipe-score-dot" />
          共鳴指數 {match.matchScore}%
        </p>

        {showProfile && (
          <>
            {/* 個性／外貌／感情狀態只在展開時出現：收合卡的高度剛好一屏，
                多塞三組標籤會把「看更多」那行擠掉。 */}
            {match.personalityTags.length > 0 && (
              <div className="swipe-field">
                <p className="swipe-field-label">個性</p>
                <div className="swipe-tags">
                  {match.personalityTags.map(tag => (
                    <span key={tag} className="swipe-tag">{tag}</span>
                  ))}
                </div>
              </div>
            )}

            {match.appearanceTags.length > 0 && (
              <div className="swipe-field">
                <p className="swipe-field-label">外貌</p>
                <div className="swipe-tags">
                  {match.appearanceTags.map(tag => (
                    <span key={tag} className="swipe-tag">{tag}</span>
                  ))}
                </div>
              </div>
            )}

            {match.relationship && (
              <div className="swipe-field">
                <p className="swipe-field-label">感情狀態</p>
                <div className="swipe-tags">
                  <span className="swipe-tag">{match.relationship}</span>
                </div>
              </div>
            )}

            <div className="profile-sheet-icebreaker">
              <p className="swipe-today-label">破冰問題</p>
              <p className="profile-sheet-icebreaker-text">「{match.icebreaker}」</p>
            </div>

            <div className="swipe-profile-actions">
              <button
                className="swipe-action-btn nope"
                onClick={e => {
                  e.stopPropagation()
                  setFlyDir('left')
                  setTimeout(() => onSwipe('left'), 320)
                }}
              >
                <svg width="22" height="22" viewBox="0 0 22 22" fill="none">
                  <path d="M5 5L17 17M17 5L5 17" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round"/>
                </svg>
                跳過
              </button>
              <button
                className="swipe-action-btn like"
                onClick={e => {
                  e.stopPropagation()
                  setFlyDir('right')
                  setTimeout(() => onSwipe('right'), 320)
                }}
              >
                <svg width="22" height="22" viewBox="0 0 22 22" fill="none">
                  <path d="M11 17C11 17 4 13 4 8.5C4 6.5 5.5 5 7.5 5C9 5 10.2 5.8 11 7C11.8 5.8 13 5 14.5 5C16.5 5 18 6.5 18 8.5C18 13 11 17 11 17Z" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"/>
                </svg>
                同頻
              </button>
            </div>

            <button
              className="swipe-collapse-btn"
              onClick={e => { e.stopPropagation(); setShowProfile(false) }}
            >
              收合介紹
            </button>
          </>
        )}

        {!showProfile && (
          <p className="swipe-tap-hint">看更多關於他今天的狀態 ↓</p>
        )}
      </div>
    </div>
  )
}

function SwipeTutorial({ onStart }: { onStart: () => void }) {
  return (
    <div className="swipe-tutorial">
      <div className="swipe-tutorial-inner">
        <h2 className="swipe-tutorial-title">找找今天同頻的人</h2>
        <p className="swipe-tutorial-sub">根據你今天的心情，我們為你配對了這些人</p>
        <div className="swipe-tutorial-illustration">
          <svg width="260" height="180" viewBox="0 0 260 180" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect x="80" y="20" width="100" height="130" rx="16" fill="#f5faf8" stroke="#c8e0db" strokeWidth="1.5"/>
            <rect x="92" y="32" width="76" height="44" rx="10" fill="#d4ece6"/>
            <circle cx="108" cy="54" r="10" fill="#b0d8d0" opacity="0.6"/>
            <rect x="92" y="84" width="50" height="6" rx="3" fill="#ddeae8"/>
            <rect x="92" y="96" width="38" height="6" rx="3" fill="#eaf2f0"/>
            <rect x="92" y="108" width="44" height="6" rx="3" fill="#eaf2f0"/>
            <rect x="18" y="32" width="80" height="104" rx="14" fill="#fdf0ef" stroke="#f0c8c4" strokeWidth="1.2" opacity="0.7" transform="rotate(-10 58 84)"/>
            <rect x="162" y="32" width="80" height="104" rx="14" fill="#edf8f4" stroke="#b8ddd6" strokeWidth="1.2" opacity="0.7" transform="rotate(10 202 84)"/>
            <g opacity="0.75">
              <path d="M52 158 L28 158" stroke="#dc9090" strokeWidth="2" strokeLinecap="round"/>
              <path d="M34 152 L28 158 L34 164" stroke="#dc9090" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
              <text x="30" y="175" fontSize="11" fill="#dc9090" textAnchor="middle" fontWeight="600">跳過</text>
            </g>
            <g opacity="0.75">
              <path d="M208 158 L232 158" stroke="#5fc4a8" strokeWidth="2" strokeLinecap="round"/>
              <path d="M226 152 L232 158 L226 164" stroke="#5fc4a8" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
              <text x="230" y="175" fontSize="11" fill="#5fc4a8" textAnchor="middle" fontWeight="600">同頻</text>
            </g>
          </svg>
        </div>
        <div className="swipe-tutorial-tips">
          <div className="swipe-tip">
            <span className="swipe-tip-icon nope">✕</span>
            <span>左滑跳過</span>
          </div>
          <div className="swipe-tip-divider" />
          <div className="swipe-tip">
            <span className="swipe-tip-icon like">♥</span>
            <span>右滑同頻</span>
          </div>
        </div>
        <p className="swipe-tutorial-note">雙方都右滑，才會開啟聊天</p>
        <button className="swipe-tutorial-btn" onClick={onStart}>開始探索</button>
      </div>
    </div>
  )
}

// 配對偏好：只放會真正改變卡片池的硬條件（想認識的對象 / 年齡）。
// 交友目的、語言、活動時段等屬於自我描述，留在「我的」頁，不在這裡過濾。
function PreferenceSheet({ userId, onClose, onApplied }: { userId: string; onClose: () => void; onApplied: () => void }) {
  const [lookingFor, setLookingFor] = useState('')
  // 滑桿一定有值，沒有「空白＝不限」這個狀態。ageTouched 用來分辨
  // 「使用者真的設過」與「只是打開看看」——沒動過就不送這兩個欄位，
  // 否則會把老使用者原本未設定的年齡偏好寫成 18/99。
  const [ageMin, setAgeMin] = useState(AGE_FLOOR)
  const [ageMax, setAgeMax] = useState(AGE_CEIL)
  const [ageTouched, setAgeTouched] = useState(false)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    getProfile(userId).then(res => {
      setLookingFor(res.data.lookingFor ?? '')
      setAgeMin(res.data.ageMin ?? AGE_FLOOR)
      setAgeMax(res.data.ageMax ?? AGE_CEIL)
      if (res.data.ageMin != null || res.data.ageMax != null) setAgeTouched(true)
    }).catch(() => {})
  }, [userId])

  async function apply() {
    setSaving(true)
    try {
      await updateProfile(userId, {
        lookingFor: lookingFor || undefined,
        // 沒動過就不帶這兩個欄位（undefined 而非 null）——後端對 null
        // 本來就是忽略，送 undefined 語意更清楚，也不會動到既有設定。
        ageMin: ageTouched ? ageMin : undefined,
        ageMax: ageTouched ? ageMax : undefined,
      })
    } catch { /* 存檔失敗不阻擋 */ }
    setSaving(false)
    onApplied()
    onClose()
  }

  return (
    <div className="pref-sheet-mask" onClick={onClose}>
      <div className="pref-sheet" role="dialog" aria-label="配對偏好" onClick={e => e.stopPropagation()}>
        <div className="pref-sheet-head">
          <span>調整我的配對偏好</span>
          <button className="pref-sheet-close" type="button" onClick={onClose} aria-label="關閉"><X size={18} /></button>
        </div>

        <p className="pref-sheet-note">調整後會同步更新你的個人檔案。</p>

        <p className="pref-field-label">想認識的對象</p>
        <div className="pref-chips">
          {LOOKING_FOR_OPTIONS.map(o => (
            <button key={o} type="button" className={`pref-chip${lookingFor === o ? ' selected' : ''}`} onClick={() => setLookingFor(v => v === o ? '' : o)}>{o}</button>
          ))}
        </div>

        <AgeRangeSlider
          className="pref-age-range"
          min={ageMin}
          max={ageMax}
          onChange={(lo, hi) => { setAgeMin(lo); setAgeMax(hi); setAgeTouched(true) }}
        />

        <button className="pref-apply-btn" type="button" disabled={saving} onClick={apply}>
          {saving ? '套用中⋯' : '套用並重新配對'}
        </button>
      </div>
    </div>
  )
}

export default function MatchResultPage({ onBack, onGoToRant, moodLabel, pendingEntry }: Props) {
  const { user } = useAuth()
  const [phase, setPhase] = useState<'loading' | 'tutorial' | 'done'>('loading')
  const [matches, setMatches] = useState<EnrichedMatch[]>([])
  const [currentIndex, setCurrentIndex] = useState(0)
  const navigate = useNavigate()
  // 配對成功時要秀的慶祝畫面；null 表示不顯示
  const [matched, setMatched] = useState<{ name: string; photo?: string; chatRoomId?: string | null } | null>(null)
  const [lastDir, setLastDir] = useState<SwipeDir>(null)
  const [prefOpen, setPrefOpen] = useState(false)
  // 右滑沒送出去時要講一聲，不能讓使用者以為表達過了
  const [toast, setToast] = useState('')
  // 今日沒存成功。下面的卡還是會顯示，但它們是依先前的狀態算的，要講清楚。
  const [entryFailed, setEntryFailed] = useState(false)
  // toast 自己消失。房內用的是同一套（ChatRoom.tsx 的 toastTimer）。
  useEffect(() => {
    if (!toast) return
    const timer = setTimeout(() => setToast(''), 3000)
    return () => clearTimeout(timer)
  }, [toast])


  // 已滑掉但表態還在路上的卡。伺服器此刻還不知道它們被滑過，
  // 所以重新載入（離開再進來、按「套用並重新配對」）時要自己濾掉，
  // 否則剛滑掉的人會立刻再出現一次——正是這次要修的問題。
  const pendingRef = useRef<Set<string>>(new Set())

  const loadMatches = useCallback(async () => {
    if (!user) return

    // 上次沒送出去的右滑先補送。後端的 Like 是冪等的（已右滑過就不再發通知），
    // 所以重送安全；不補的話那幾次同頻等於從未發生，對方永遠配不到。
    for (const matchId of readPendingLikes()) {
      try {
        await likeMatch(matchId)
        clearPendingLike(matchId)
      } catch (err) {
        // 對方已經不在了就從佇列拿掉。留著只是每次進頁面都再送一次、
        // 再失敗一次，一路送到七天後自己過期為止。
        if (isCandidateGone(err)) clearPendingLike(matchId)
        // 其他原因（沒網路、伺服器還在冷啟動）才留著下次再試
      }
    }

    // 先等「存今日」落地再取卡。失敗不擋流程——還是給卡，但要讓使用者知道
    // 這批卡不是依他剛寫的內容算的，而且他寫的東西沒存上。
    if (pendingEntry) {
      try {
        await pendingEntry
      } catch {
        setEntryFailed(true)
      }
    }

    try {
      const res = await getTodayMatches(user.userId)
      // 只濾掉左滑過的。右滑失敗的人不能濾——濾掉就等於幫使用者把人藏起來，
      // 而伺服器根本沒收到那次同頻。
      const skip = readPassedIds()
      pendingRef.current.forEach((id) => skip.add(id))
      setMatches(res.data.filter((m) => !skip.has(m.matchId)).map(mapMatch))
    } catch {
      setMatches([])
    }
    setCurrentIndex(0)
    setLastDir(null)
  }, [user, pendingEntry])

  useEffect(() => {
    void (async () => {
      await loadMatches()
      const seen = localStorage.getItem(TUTORIAL_KEY)
      setPhase(seen ? 'done' : 'tutorial')
    })()
  }, [loadMatches])

  function handleSwipe(dir: SwipeDir) {
    const card = matches[currentIndex]
    // 畫面先走，網路在背景跑。滑卡手感優先，不給 loading、不擋 UI。
    setLastDir(dir)
    setCurrentIndex((i) => i + 1)
    if (card) void recordDecision(card, dir)
  }

  async function recordDecision(card: EnrichedMatch, dir: SwipeDir) {
    pendingRef.current.add(card.matchId)
    try {
      if (dir === 'right') {
        // 右滑當下不重試：httpClient 明文規定寫入請求不重試，重送會讓對方
        // 收到第二則「你們互相同頻了」。失敗改排進佇列，下次進頁面補送。
        // 只有「雙方都按了同頻」才跳慶祝畫面：單向喜歡時對方還不知道這件事。
        const res = await likeMatch(card.matchId)
        if (res.data?.isMutual) {
          setMatched({ name: card.nickname, photo: card.photos[0], chatRoomId: res.data.chatRoomId })
        }
      } else {
        // 左滑是冪等的（後端一律重新計時），而且它是「不再出現」的唯一依據，
        // 所以值得重試一次。
        await passMatch(card.matchId).catch(() => passMatch(card.matchId))
      }
    } catch (err) {
      if (dir === 'right') {
        if (isCandidateGone(err)) {
          // 補送也不會成功——卡片發到手上之後這個人被封鎖／註銷／停權了。
          // 不排進佇列，也不記進本機備援：停權期滿他會回到候選池，
          // 本機擋住反而讓他再也出不來。伺服器那邊什麼都沒寫。
          setToast('這個人已經不在了，這張卡就跳過吧')
        } else {
          // 絕不能只記在本機就算了——那會讓這次同頻靜靜消失，而且人還被擋住
          rememberPendingLike(card.matchId)
          setToast('這個同頻還沒送出，下次打開會自動補送')
        }
      } else {
        rememberPassed(card.matchId)
      }
    } finally {
      pendingRef.current.delete(card.matchId)
    }
  }

  const visibleCards = matches.slice(currentIndex, currentIndex + 3)
  const allDone = currentIndex >= matches.length

  return (
    <div className="page match-result-page">
      {toast && <div className="cr-toast" role="status">{toast}</div>}

      {/* 今日沒存成功。先前這裡什麼都不會說，使用者寫的東西靜悄悄不見，
          而且下面這批卡其實是依他「上一則」今日算出來的。 */}
      {entryFailed && (
        <div className="match-entry-failed" role="alert">
          <p>今天寫的內容沒有存成功，下面的配對是依你先前的狀態算的。</p>
          <button type="button" onClick={onBack}>回去重寫</button>
        </div>
      )}
      {matched && (
        <MatchCelebration
          name={matched.name}
          photo={matched.photo}
          onClose={() => setMatched(null)}
          onGoChat={matched.chatRoomId
            ? () => { const id = matched.chatRoomId!; setMatched(null); navigate(`/chat?room=${encodeURIComponent(id)}`) }
            : undefined}
        />
      )}

      {phase === 'loading' && (
        <div className="match-loading-mask">
          <div className="match-spinner-ring" />
          <p className="match-loading-text">正在尋找今天同頻的人…</p>
        </div>
      )}

      {phase === 'tutorial' && (
        <SwipeTutorial onStart={() => {
          localStorage.setItem(TUTORIAL_KEY, '1')
          setPhase('done')
        }} />
      )}

      {phase === 'done' && (
        <div className="match-result-content">
          <header className="match-result-header">
            <button className="match-back-btn" onClick={onBack} aria-label="返回">
              <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
                <path d="M12.5 15L7.5 10L12.5 5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              返回
            </button>
            <button className="match-pref-btn" onClick={() => setPrefOpen(true)} aria-label="調整偏好">
              <SlidersHorizontal size={18} />
              篩選
            </button>
          </header>

          {allDone ? (
            <div className="match-empty-state">
              <div className="match-empty-icon">
                <svg width="72" height="72" viewBox="0 0 72 72" fill="none">
                  <circle cx="36" cy="36" r="32" fill="#f0f7f4" />
                  <path d="M24 42c0-6.627 5.373-12 12-12s12 5.373 12 12" stroke="#a8c8c2" strokeWidth="2" strokeLinecap="round" />
                  <circle cx="28" cy="30" r="2.5" fill="#b8d4ce" />
                  <circle cx="44" cy="30" r="2.5" fill="#b8d4ce" />
                </svg>
              </div>
              <p className="match-empty-title">今天先看到這裡。</p>
              <p className="match-empty-desc">
                {moodLabel ? `今天${moodLabel}的你，已經把能看的人都看過了。` : '能看的人都看過了。'}
              </p>
              <p className="match-empty-desc">
                略過的人過陣子會回到這裡，新朋友發了「今天」也會出現。
                先去樹洞看看，或把偏好放寬一點？
              </p>
              <div className="match-empty-actions">
                <button className="match-empty-rant" onClick={onGoToRant}>去樹洞看看</button>
                <button className="match-empty-back" onClick={() => setPrefOpen(true)}>調整偏好</button>
              </div>
            </div>
          ) : (
            <>
              <div className="swipe-stack-wrap">
                <div className="swipe-stack">
                  {[...visibleCards].reverse().map((match, i) => {
                    const stackOffset = visibleCards.length - 1 - i
                    const isTop = stackOffset === 0
                    return (
                      <SwipeCard
                        key={match.matchId}
                        match={match}
                        isTop={isTop}
                        stackOffset={stackOffset}
                        onSwipe={isTop ? handleSwipe : () => {}}
                      />
                    )
                  })}
                </div>
              </div>

              {lastDir === 'right' && (
                <p className="swipe-hint">✦ 已標記同頻，等對方也右滑就能聊天</p>
              )}
            </>
          )}
        </div>
      )}

      {prefOpen && user && (
        <PreferenceSheet
          userId={user.userId}
          onClose={() => setPrefOpen(false)}
          onApplied={() => { setPhase('loading'); void loadMatches().then(() => setPhase('done')) }}
        />
      )}
    </div>
  )
}


/**
 * 配對成功的慶祝畫面。
 *
 * 只在雙方都按下同頻時出現。兩個頭像從左右滑進來、中間的愛心彈跳一下，
 * 下方給「開始聊天」直接切到那間已經自動開好的房。
 * 開房失敗時不給按鈕（onGoChat 為 undefined），只恭喜一下就好。
 */
function MatchCelebration({ name, photo, onClose, onGoChat }: {
  name: string
  photo?: string
  onClose: () => void
  onGoChat?: () => void
}) {
  return (
    <div className="match-celebrate-backdrop" role="dialog" aria-modal="true" aria-label="配對成功">
      <div className="match-celebrate">
        <div className="match-celebrate-avatars">
          <span className="match-celebrate-avatar from-left">
            <span className="match-celebrate-avatar-inner you">你</span>
          </span>
          <span className="match-celebrate-heart" aria-hidden="true">♥</span>
          <span className="match-celebrate-avatar from-right">
            {photo
              ? <img src={photo} alt="" className="match-celebrate-avatar-inner" />
              : <span className="match-celebrate-avatar-inner">{name.trim()[0] ?? '?'}</span>}
          </span>
        </div>

        <p className="match-celebrate-title">你們互相同頻了</p>
        <p className="match-celebrate-sub">你和 {name} 都按下了同頻</p>

        {onGoChat && (
          <button className="match-celebrate-chat" type="button" onClick={onGoChat}>
            開始聊天
          </button>
        )}
        <button className="match-celebrate-later" type="button" onClick={onClose}>
          繼續看看
        </button>
      </div>
    </div>
  )
}
