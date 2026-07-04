import { useEffect, useRef, useState, useCallback } from 'react'
import { SlidersHorizontal, X } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { getTodayMatches, likeMatch } from './api'
import { getProfile, updateProfile } from '../profile/api'
import { getZodiac, getAge } from '../profile/profileUtils'
import type { MatchResult } from './types'

type EnrichedMatch = Omit<MatchResult, 'photos' | 'interests'> & {
  photos: string[]
  interests: string[]
  age?: number
  city?: string
  bio?: string
  zodiac?: string
}

const TUTORIAL_KEY = 'match-swipe-tutorial-done'
const DATING_GOAL_OPTIONS = ['交友', '約會', '長期關係', '先聊聊'] as const
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
        const relY = e.clientY - rect.top
        const photoAreaHeight = rect.width * 1.05
        if (relY < photoAreaHeight) {
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

        <p className="swipe-today-label">今天想說的話</p>
        <p className={`swipe-today-text${showProfile ? '' : ' clamped'}`}>
          「{match.todaySummary}」
        </p>

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

// 快速偏好：滑卡當下微調交友意圖，存檔後立即重刷卡片
function PreferenceSheet({ userId, onClose, onApplied }: { userId: string; onClose: () => void; onApplied: () => void }) {
  const [datingGoal, setDatingGoal] = useState('')
  const [lookingFor, setLookingFor] = useState('')
  const [ageMin, setAgeMin] = useState('')
  const [ageMax, setAgeMax] = useState('')
  const [distanceKm, setDistanceKm] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    getProfile(userId).then(res => {
      setDatingGoal(res.data.datingGoal ?? '')
      setLookingFor(res.data.lookingFor ?? '')
      setAgeMin(res.data.ageMin != null ? String(res.data.ageMin) : '')
      setAgeMax(res.data.ageMax != null ? String(res.data.ageMax) : '')
      setDistanceKm(res.data.distanceKm != null ? String(res.data.distanceKm) : '')
    }).catch(() => {})
  }, [userId])

  async function apply() {
    setSaving(true)
    try {
      await updateProfile(userId, {
        datingGoal: datingGoal || undefined,
        lookingFor: lookingFor || undefined,
        ageMin: ageMin ? Number(ageMin) : null,
        ageMax: ageMax ? Number(ageMax) : null,
        distanceKm: distanceKm ? Number(distanceKm) : null,
      })
    } catch { /* 存檔失敗不阻擋 */ }
    setSaving(false)
    onApplied()
    onClose()
  }

  return (
    <div className="pref-sheet-mask" onClick={onClose}>
      <div className="pref-sheet" role="dialog" aria-label="快速偏好" onClick={e => e.stopPropagation()}>
        <div className="pref-sheet-head">
          <span>調整想認識的人</span>
          <button className="pref-sheet-close" type="button" onClick={onClose} aria-label="關閉"><X size={18} /></button>
        </div>

        <p className="pref-field-label">交友目的</p>
        <div className="pref-chips">
          {DATING_GOAL_OPTIONS.map(o => (
            <button key={o} type="button" className={`pref-chip${datingGoal === o ? ' selected' : ''}`} onClick={() => setDatingGoal(v => v === o ? '' : o)}>{o}</button>
          ))}
        </div>

        <p className="pref-field-label">想認識的對象</p>
        <div className="pref-chips">
          {LOOKING_FOR_OPTIONS.map(o => (
            <button key={o} type="button" className={`pref-chip${lookingFor === o ? ' selected' : ''}`} onClick={() => setLookingFor(v => v === o ? '' : o)}>{o}</button>
          ))}
        </div>

        <p className="pref-field-label">年齡範圍</p>
        <div className="pref-range">
          <input type="number" inputMode="numeric" min={18} max={99} placeholder="最小" value={ageMin} onChange={e => setAgeMin(e.target.value)} />
          <span>—</span>
          <input type="number" inputMode="numeric" min={18} max={99} placeholder="最大" value={ageMax} onChange={e => setAgeMax(e.target.value)} />
          <span className="pref-unit">歲</span>
        </div>

        <p className="pref-field-label">距離偏好</p>
        <div className="pref-range">
          <input type="number" inputMode="numeric" min={1} max={500} placeholder="距離" value={distanceKm} onChange={e => setDistanceKm(e.target.value)} />
          <span className="pref-unit">km</span>
        </div>

        <button className="pref-apply-btn" type="button" disabled={saving} onClick={apply}>
          {saving ? '套用中⋯' : '套用並重新配對'}
        </button>
      </div>
    </div>
  )
}

export default function MatchResultPage({ onBack, onGoToRant, moodLabel }: Props) {
  const { user } = useAuth()
  const [phase, setPhase] = useState<'loading' | 'tutorial' | 'done'>('loading')
  const [matches, setMatches] = useState<EnrichedMatch[]>([])
  const [currentIndex, setCurrentIndex] = useState(0)
  const [lastDir, setLastDir] = useState<SwipeDir>(null)
  const [prefOpen, setPrefOpen] = useState(false)

  const loadMatches = useCallback(async () => {
    if (!user) return
    try {
      const res = await getTodayMatches(user.userId)
      setMatches(res.data.map(mapMatch))
    } catch {
      setMatches([])
    }
    setCurrentIndex(0)
    setLastDir(null)
  }, [user])

  useEffect(() => {
    void (async () => {
      await loadMatches()
      const seen = localStorage.getItem(TUTORIAL_KEY)
      setPhase(seen ? 'done' : 'tutorial')
    })()
  }, [loadMatches])

  function handleSwipe(dir: SwipeDir) {
    const card = matches[currentIndex]
    if (dir === 'right' && card) void likeMatch(card.matchId).catch(() => {})
    setLastDir(dir)
    setCurrentIndex((i) => i + 1)
  }

  const visibleCards = matches.slice(currentIndex, currentIndex + 3)
  const allDone = currentIndex >= matches.length

  return (
    <div className="page match-result-page">
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
              <p className="match-empty-title">今天的緣分到這裡了。</p>
              <p className="match-empty-desc">
                {moodLabel ? `今天${moodLabel}的你，已經把今天的人都看過了。` : '今天的人都看過了。'}
              </p>
              <p className="match-empty-desc">去樹洞看看今天還有誰在，或調整偏好再試試？</p>
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
