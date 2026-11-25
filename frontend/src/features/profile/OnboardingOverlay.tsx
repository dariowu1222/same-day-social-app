import { useRef, useState } from 'react'
import type { DemoUser } from '../auth/types'
import { updateProfile } from './api'
import AgeRangeSlider, { AGE_CEIL, AGE_FLOOR } from '../../shared/ui/AgeRangeSlider'
import LocationPrimer from './LocationPrimer'
import {
  AREA_OPTIONS,
  DATING_GOAL_OPTIONS,
  DISTANCE_KM_MAX,
  DISTANCE_KM_MIN,
  GENDER_OPTIONS,
  LOOKING_FOR_OPTIONS,
  RELATIONSHIP_OPTIONS,
  type DistanceMode,
} from './profileFields'
import SyncHero from '../../shared/ui/SyncHero'

// 定位說明頁排在功能介紹之後、填資料之前：
// 看完介紹才知道這個 App 在做什麼，這時候問定位才有理由；
// 排在填資料之前，後面「你在哪個縣市」就能直接帶入，不用再選一次。
type Phase = 'slides' | 'location' | 'setup' | 'intent'

// 功能介紹三頁，畫面上是同一對圓走完一個故事：
// 各自一邊（今天過得怎麼樣）→ 靠近（遇見同頻的人）→ 合成愛心（兩個人都同頻才開始聊）。
// 跟歡迎頁、配對成立用的是同一組角色。
//
// 以前是五頁、每頁一張插圖（窗邊的咖啡、筆記本……），跟產品其他地方不是同一個畫風；
// 最後一頁還在介紹已經下架的「小任務」。
const SLIDES = [
  {
    title: ['今天，', '你過得怎麼樣？'],
    desc: '每天寫一句話、選一個心情，像跟朋友說話一樣就好',
  },
  {
    title: ['遇見今天', '跟你同頻的人'],
    desc: '我們找出今天和你有共鳴的人，並且告訴你為什麼是他',
  },
  {
    title: ['兩個人都同頻，', '才開始聊'],
    desc: '不想配對的日子，也可以把話放進樹洞，會有人接住',
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
  // 年齡範圍：滑桿一定有值，所以另外記「有沒有真的動過」，沒動過就不送出。
  //
  // 以前這裡預設 18–40，而且不管有沒有動、甚至按「先跳過」都會寫進去。
  // 於是每個人身上都有一組自己沒選過的年齡範圍：41 歲以上的人被濾掉，
  // 沒填生日的人也被濾掉（配對拿生日比年齡，對不上就排除）——真人之間幾乎互相看不到。
  // 預設改成整條滑桿（18–99），畫面上看到的就是實際的效果：沒動，就是不限。
  const [ageMin, setAgeMin] = useState(AGE_FLOOR)
  const [ageMax, setAgeMax] = useState(AGE_CEIL)
  const [ageTouched, setAgeTouched] = useState(false)
  const [locationArea, setLocationArea] = useState('')
  // 所在縣市是定位帶出來的、還是使用者自己選的。只影響那一格底下的說明文字。
  const [areaFromLocation, setAreaFromLocation] = useState(false)
  const [distanceKm, setDistanceKm] = useState('')
  const [distanceMode, setDistanceMode] = useState<DistanceMode>('km')
  const [preferredArea, setPreferredArea] = useState('')
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
    else setPhase('location')
  }

  function handleLocationDone(area: string | null) {
    if (area) {
      setLocationArea(area)
      setAreaFromLocation(true)
    }
    setPhase('setup')
  }

  function toggleTag(tag: string) {
    setSelectedTags(prev =>
      prev.includes(tag) ? prev.filter(t => t !== tag) : [...prev, tag],
    )
  }

  async function handleComplete() {
    if (savingRef.current) return
    savingRef.current = true

    setSaving(true)
    try {
      await updateProfile(user.userId, {
        gender: gender || undefined,
        relationship: relationship || undefined,
        bio: bio.trim() || undefined,
        interestTags: selectedTags.length > 0 ? selectedTags : undefined,
        datingGoal: datingGoal || undefined,
        lookingFor: lookingFor || undefined,
        ageMin: ageTouched ? ageMin : undefined,
        ageMax: ageTouched ? ageMax : undefined,
        // 自己在哪。依距離要靠它當起點，別人指定縣市時也是拿它比對——
        // 沒填的人對所有設了地區偏好的人都是隱形的。
        locationArea: locationArea || undefined,
        // 依距離或依縣市二擇一：只送目前模式的值（後端寫入其中一個就會清掉另一個）
        distanceKm: distanceMode === 'km' ? clampOptionalNumber(distanceKm, DISTANCE_KM_MIN, DISTANCE_KM_MAX) : undefined,
        preferredArea: distanceMode === 'area' ? (preferredArea.trim() || undefined) : undefined,
      })
    } catch {
      // 儲存失敗不阻擋流程
    }
    setSaving(false)
    savingRef.current = false
    onComplete()
  }

  const slide = SLIDES[current]

  if (phase === 'location') {
    return (
      <div className="onboarding-overlay">
        <LocationPrimer onDone={handleLocationDone} />
      </div>
    )
  }

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
              <AgeRangeSlider
                min={ageMin}
                max={ageMax}
                onChange={(lo, hi) => { setAgeMin(lo); setAgeMax(hi); setAgeTouched(true) }}
              />
            </div>

            <div className="ob-field">
              <span className="ob-field-label">你在哪個縣市</span>
              <select
                className="ob-select"
                aria-label="所在縣市"
                value={locationArea}
                onChange={event => { setLocationArea(event.target.value); setAreaFromLocation(false) }}
              >
                <option value="">先不填</option>
                {AREA_OPTIONS.map(area => (
                  <option key={area} value={area}>{area}</option>
                ))}
              </select>
              {/* 定位在縣市交界可能差一點，講明這是帶入的、可以改 */}
              {areaFromLocation && locationArea && (
                <span className="profile-hint profile-hint-block">依定位帶入，不對可以改</span>
              )}
            </div>

            <div className="ob-field">
              <span className="ob-field-label">距離偏好</span>
              <div className="ob-segment" role="group" aria-label="距離偏好方式">
                <button
                  type="button"
                  className={`ob-segment-btn${distanceMode === 'km' ? ' active' : ''}`}
                  onClick={() => setDistanceMode('km')}
                >依距離</button>
                <button
                  type="button"
                  className={`ob-segment-btn${distanceMode === 'area' ? ' active' : ''}`}
                  onClick={() => setDistanceMode('area')}
                >依縣市</button>
              </div>
              {distanceMode === 'km' ? (
                <div className="profile-field-unit">
                  <input
                    className="ob-number-input"
                    type="number"
                    inputMode="numeric"
                    min={DISTANCE_KM_MIN}
                    max={DISTANCE_KM_MAX}
                    placeholder="距離"
                    aria-label="距離偏好"
                    value={distanceKm}
                    onChange={event => setDistanceKm(event.target.value)}
                  />
                  <span className="profile-unit">km</span>
                </div>
              ) : (
                <select
                  className="ob-select"
                  aria-label="偏好縣市"
                  value={preferredArea}
                  onChange={event => setPreferredArea(event.target.value)}
                >
                  <option value="">不限縣市</option>
                  {AREA_OPTIONS.map(area => (
                    <option key={area} value={area}>{area}</option>
                  ))}
                </select>
              )}
              {/* 距離是縣市對縣市的估算，不是定位。講清楚，免得填了 5 公里的人
                  以為隔壁縣市的人一定近在咫尺；沒有起點時也直接說，而不是讓它安靜地不生效。 */}
              {distanceMode === 'km' && (
                <span className="profile-hint profile-hint-block">
                  {locationArea ? '以雙方所在的縣市估算，同縣市一律算在內' : '要先選上面的所在縣市，才算得出距離'}
                </span>
              )}
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
              <span className="ob-field-label">性別 <span className="profile-required">*</span></span>
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
              {!gender ? '請先選性別' : '下一步 →'}
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
    <div className="onboarding-overlay ob-intro">
      {/* 跳過的是功能介紹，不是定位說明頁——那一頁有自己的「先不用」 */}
      <button className="ob-skip-btn" type="button" onClick={() => setPhase('location')}>
        跳過
      </button>

      {/* 左右滑也能換頁；畫面本身不跟著手指移動，只有兩顆圓會動 */}
      <div
        className="ob-intro-main"
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerCancel={onPointerUp}
        style={{ touchAction: 'pan-y' }}
      >
        <div className="ob-intro-stage" aria-hidden="true">
          <p className={`ob-intro-note${current === 0 ? ' is-on' : ''}`}>加班到九點，走出公司才發現在下雨</p>
          <SyncHero near={current === 1} merged={current === 2} />
        </div>
        <div className="ob-intro-text" key={current}>
          <h2 className="ob-slide-title">{slide.title[0]}<br />{slide.title[1]}</h2>
          <p className="ob-slide-desc">{slide.desc}</p>
        </div>
      </div>

      <div className="ob-controls">
        <div className="ob-dots" aria-label="投影片進度">
          {SLIDES.map((_, i) => (
            <button
              key={i}
              type="button"
              className={`ob-dot${i === current ? ' active' : ''}`}
              onClick={() => setCurrent(i)}
              aria-label={`第 ${i + 1} 頁`}
            />
          ))}
        </div>
        <button className="keycap-btn" type="button" onClick={goNext}>
          {current === total - 1 ? '開始設定' : '下一頁'}
          <span className="keycap-arrow" aria-hidden="true" />
        </button>
      </div>
    </div>
  )
}
