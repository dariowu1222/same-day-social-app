import { useRef, useState } from 'react'
import type { DemoUser } from '../auth/types'
import { updateProfile } from './api'
import {
  DATING_GOAL_OPTIONS,
  GENDER_OPTIONS,
  LOOKING_FOR_OPTIONS,
  RELATIONSHIP_OPTIONS,
} from './profileFields'
import slide1 from '../../assets/1.webp'
import slide2 from '../../assets/2.webp'
import slide3 from '../../assets/3.webp'
import slide4 from '../../assets/4.webp'
import slide5 from '../../assets/5.webp'

type Phase = 'slides' | 'setup' | 'intent'

const SLIDES = [
  {
    img: slide1,
    bg: 'linear-gradient(160deg, #fff3d8 0%, #ffe4bb 100%)',
    accent: '#ef8968',
    title: '今天，你過得怎麼樣？',
    desc: '同頻 Today 從每天一件小事開始，讓你慢慢認識和你同頻的人',
  },
  {
    img: slide2,
    bg: 'linear-gradient(160deg, #e8f5f0 0%, #d7efe9 100%)',
    accent: '#4e9188',
    title: '記下今天的故事',
    desc: '不用長篇大論，一句話、一個心情，就能開始你的今日共鳴',
  },
  {
    img: slide3,
    bg: 'linear-gradient(160deg, #fff8d8 0%, #fef0b0 100%)',
    accent: '#e8a645',
    title: '找到同頻的人',
    desc: '系統根據你的今日記錄，配對有共鳴的人，不強迫、不勉強',
  },
  {
    img: slide4,
    bg: 'linear-gradient(160deg, #eaf5f0 0%, #c8e8e0 100%)',
    accent: '#4e9188',
    title: '說說心裡話',
    desc: '樹洞讓你匿名傾訴今天的煩惱，有人懂你，有人陪你',
  },
  {
    img: slide5,
    bg: 'linear-gradient(160deg, #fff3d8 0%, #ffe4bb 100%)',
    accent: '#ef8968',
    title: '低壓開始，慢慢認識',
    desc: '透過小任務和聊天，用自己的步調認識新朋友',
  },
]

const INTEREST_OPTIONS = [
  '咖啡', '散步', '電影', '音樂', '閱讀', '料理',
  '旅遊', '攝影', '運動', '遊戲', '動漫', 'Podcast',
  '貓狗', '手作', '冥想', '追劇', '寫作', '展覽',
]

function clampOptionalNumber(value: string, min: number, max: number) {
  if (!value) return null
  const parsed = Number(value)
  if (!Number.isFinite(parsed)) return null
  return Math.min(max, Math.max(min, parsed))
}

type Props = {
  user: DemoUser
  onComplete: () => void
}

export default function OnboardingOverlay({ user, onComplete }: Props) {
  const [phase, setPhase] = useState<Phase>('slides')
  const [current, setCurrent] = useState(0)
  const [dragOffset, setDragOffset] = useState(0)
  const [dragging, setDragging] = useState(false)
  const pointerStartX = useRef(0)
  const pointerId = useRef<number | null>(null)

  const [gender, setGender] = useState('')
  const [relationship, setRelationship] = useState('')
  const [bio, setBio] = useState('')
  const [selectedTags, setSelectedTags] = useState<string[]>([])
  const [datingGoal, setDatingGoal] = useState('')
  const [lookingFor, setLookingFor] = useState('')
  const [ageMin, setAgeMin] = useState('')
  const [ageMax, setAgeMax] = useState('')
  const [distanceKm, setDistanceKm] = useState('')
  const [saving, setSaving] = useState(false)
  const savingRef = useRef(false)

  const total = SLIDES.length

  function onPointerDown(e: React.PointerEvent) {
    pointerId.current = e.pointerId
    pointerStartX.current = e.clientX
    setDragging(true)
    ;(e.currentTarget as HTMLElement).setPointerCapture(e.pointerId)
  }

  function onPointerMove(e: React.PointerEvent) {
    if (!dragging || e.pointerId !== pointerId.current) return
    setDragOffset(e.clientX - pointerStartX.current)
  }

  function onPointerUp(e: React.PointerEvent) {
    if (!dragging || e.pointerId !== pointerId.current) return
    setDragging(false)
    if (dragOffset < -60 && current < total - 1) setCurrent(c => c + 1)
    else if (dragOffset > 60 && current > 0) setCurrent(c => c - 1)
    setDragOffset(0)
    pointerId.current = null
  }

  function goNext() {
    if (current < total - 1) setCurrent(c => c + 1)
    else setPhase('setup')
  }

  function toggleTag(tag: string) {
    setSelectedTags(prev =>
      prev.includes(tag) ? prev.filter(t => t !== tag) : [...prev, tag],
    )
  }

  async function handleComplete() {
    if (savingRef.current) return
    savingRef.current = true

    let normalizedAgeMin = clampOptionalNumber(ageMin, 18, 99)
    let normalizedAgeMax = clampOptionalNumber(ageMax, 18, 99)
    if (normalizedAgeMin != null && normalizedAgeMax != null && normalizedAgeMin > normalizedAgeMax) {
      ;[normalizedAgeMin, normalizedAgeMax] = [normalizedAgeMax, normalizedAgeMin]
    }

    setSaving(true)
    try {
      await updateProfile(user.userId, {
        gender: gender || undefined,
        relationship: relationship || undefined,
        bio: bio.trim() || undefined,
        interestTags: selectedTags.length > 0 ? selectedTags : undefined,
        datingGoal: datingGoal || undefined,
        lookingFor: lookingFor || undefined,
        ageMin: normalizedAgeMin,
        ageMax: normalizedAgeMax,
        distanceKm: clampOptionalNumber(distanceKm, 1, 500),
      })
    } catch {
      // 儲存失敗不阻擋流程
    }
    setSaving(false)
    savingRef.current = false
    onComplete()
  }

  const trackX = -current * 100 + (dragOffset / window.innerWidth) * 100
  const slide = SLIDES[current]

  if (phase === 'intent') {
    return (
      <div className="onboarding-overlay">
        <div className="onboarding-setup">
          <div className="ob-setup-top">
            <h1>想認識什麼樣的人？</h1>
            <p>這些只影響配對推薦，之後都能在「我的」修改</p>
          </div>

          <div className="ob-setup-body">
            <div className="ob-field">
              <span className="ob-field-label">交友目的</span>
              <div className="ob-tags">
                {DATING_GOAL_OPTIONS.map(option => (
                  <button
                    key={option}
                    type="button"
                    className={`ob-tag${datingGoal === option ? ' selected' : ''}`}
                    onClick={() => setDatingGoal(value => value === option ? '' : option)}
                  >
                    {option}
                  </button>
                ))}
              </div>
            </div>

            <div className="ob-field">
              <span className="ob-field-label">想認識的對象</span>
              <div className="ob-tags">
                {LOOKING_FOR_OPTIONS.map(option => (
                  <button
                    key={option}
                    type="button"
                    className={`ob-tag${lookingFor === option ? ' selected' : ''}`}
                    onClick={() => setLookingFor(value => value === option ? '' : option)}
                  >
                    {option}
                  </button>
                ))}
              </div>
            </div>

            <div className="ob-field">
              <span className="ob-field-label">年齡範圍</span>
              <div className="profile-field-row">
                <div className="profile-field-unit">
                  <input
                    className="ob-number-input"
                    type="number"
                    inputMode="numeric"
                    min={18}
                    max={99}
                    placeholder="最小"
                    aria-label="最小年齡"
                    value={ageMin}
                    onChange={event => setAgeMin(event.target.value)}
                  />
                  <span className="profile-unit">歲</span>
                </div>
                <div className="profile-field-unit">
                  <input
                    className="ob-number-input"
                    type="number"
                    inputMode="numeric"
                    min={18}
                    max={99}
                    placeholder="最大"
                    aria-label="最大年齡"
                    value={ageMax}
                    onChange={event => setAgeMax(event.target.value)}
                  />
                  <span className="profile-unit">歲</span>
                </div>
              </div>
            </div>

            <div className="ob-field">
              <span className="ob-field-label">距離偏好</span>
              <div className="profile-field-unit">
                <input
                  className="ob-number-input"
                  type="number"
                  inputMode="numeric"
                  min={1}
                  max={500}
                  placeholder="距離"
                  aria-label="距離偏好"
                  value={distanceKm}
                  onChange={event => setDistanceKm(event.target.value)}
                />
                <span className="profile-unit">km</span>
              </div>
            </div>
          </div>

          <div className="ob-setup-footer">
            <button
              className="ob-primary-btn"
              type="button"
              disabled={saving}
              onClick={handleComplete}
            >
              {saving ? '儲存中⋯' : '開始同頻 Today'}
            </button>
            <button
              className="ob-text-btn"
              type="button"
              disabled={saving}
              onClick={handleComplete}
            >
              先跳過，之後再設定
            </button>
          </div>
        </div>
      </div>
    )
  }

  if (phase === 'setup') {
    return (
      <div className="onboarding-overlay">
        <div className="onboarding-setup">
          <div className="ob-setup-top">
            <div className="ob-avatar" aria-hidden="true">
              {user.nickname[0]}
            </div>
            <h1>嗨，{user.nickname}！</h1>
            <p>讓大家多認識你一點，之後也可以隨時修改</p>
          </div>

          <div className="ob-setup-body">
            <div className="ob-field">
              <span className="ob-field-label">生理性別 <span className="profile-required">*</span></span>
              <div className="ob-tags">
                {GENDER_OPTIONS.map(o => (
                  <button
                    key={o}
                    type="button"
                    className={`ob-tag${gender === o ? ' selected' : ''}`}
                    onClick={() => setGender(o)}
                  >{o}</button>
                ))}
              </div>
            </div>

            <div className="ob-field">
              <span className="ob-field-label">感情狀態</span>
              <div className="ob-tags">
                {RELATIONSHIP_OPTIONS.map(o => (
                  <button
                    key={o}
                    type="button"
                    className={`ob-tag${relationship === o ? ' selected' : ''}`}
                    onClick={() => setRelationship(r => r === o ? '' : o)}
                  >{o}</button>
                ))}
              </div>
            </div>

            <div className="ob-field">
              <label htmlFor="ob-bio">自我介紹</label>
              <textarea
                id="ob-bio"
                rows={3}
                maxLength={150}
                placeholder="說說你是什麼樣的人、今天的心情，或任何想讓大家知道的……"
                value={bio}
                onChange={e => setBio(e.target.value)}
              />
              <span className="ob-field-count">{bio.length}/150</span>
            </div>

            <div className="ob-field">
              <span className="ob-field-label">興趣標籤（可多選）</span>
              <div className="ob-tags">
                {INTEREST_OPTIONS.map(tag => (
                  <button
                    key={tag}
                    type="button"
                    className={`ob-tag${selectedTags.includes(tag) ? ' selected' : ''}`}
                    onClick={() => toggleTag(tag)}
                  >
                    {tag}
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className="ob-setup-footer">
            <button
              className="ob-primary-btn"
              type="button"
              disabled={saving || !gender}
              onClick={() => setPhase('intent')}
            >
              {!gender ? '請先選生理性別' : '下一步 →'}
            </button>
            <button
              className="ob-text-btn"
              type="button"
              disabled={saving}
              onClick={handleComplete}
            >
              先跳過，之後再設定
            </button>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="onboarding-overlay">
      <div
        className="ob-slides-viewport"
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerCancel={onPointerUp}
        style={{ touchAction: 'pan-y', cursor: dragging ? 'grabbing' : 'grab' }}
      >
        <div
          className={`ob-track${dragging ? ' dragging' : ''}`}
          style={{ transform: `translateX(${trackX}%)` }}
        >
          {SLIDES.map((s, i) => (
            <div key={i} className="ob-slide" style={{ background: s.bg }}>
              <div className="ob-slide-visual" style={{ boxShadow: `0 20px 60px ${s.accent}30` }}>
                <img src={s.img} alt="" draggable={false} />
              </div>
              <h2 className="ob-slide-title" style={{ color: '#3e1d0a' }}>{s.title}</h2>
              <p className="ob-slide-desc">{s.desc}</p>
            </div>
          ))}
        </div>
      </div>

      <div className="ob-controls">
        <div className="ob-dots" aria-label="投影片進度">
          {SLIDES.map((_, i) => (
            <button
              key={i}
              type="button"
              className={`ob-dot${i === current ? ' active' : ''}`}
              style={i === current ? { background: slide.accent } : undefined}
              onClick={() => setCurrent(i)}
              aria-label={`第 ${i + 1} 頁`}
            />
          ))}
        </div>

        <div className="ob-actions">
          <button className="ob-skip-btn" type="button" onClick={() => setPhase('setup')}>
            跳過
          </button>
          <button
            className="ob-next-btn"
            type="button"
            style={{ background: slide.accent }}
            onClick={goNext}
          >
            {current === total - 1 ? '開始設定 →' : '下一頁'}
          </button>
        </div>
      </div>
    </div>
  )
}
