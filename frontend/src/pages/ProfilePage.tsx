import { useState, useEffect, useRef } from 'react'
import { Sun, Moon, Plus, X, Eye, EyeOff, Camera } from 'lucide-react'
import type { DemoUser } from '../App'
import { useTheme, type ThemePreference } from '../context/ThemeContext'
import { getProfile, updateProfile, clearAuthToken } from '../api/client'

type Props = {
  user: DemoUser
  setUser: (user: DemoUser | null) => void
}

const INTEREST_OPTIONS = [
  '咖啡', '散步', '電影', '音樂', '閱讀', '料理',
  '旅遊', '攝影', '運動', '遊戲', '動漫', 'Podcast',
  '貓狗', '手作', '冥想', '追劇', '寫作', '展覽',
]

const PREFS: { value: ThemePreference; icon?: 'sun' | 'moon'; label?: string }[] = [
  { value: 'day',   icon: 'sun' },
  { value: 'night', icon: 'moon' },
  { value: 'auto',  label: '自動' },
]

const MAX_PHOTOS = 6

// ── 測試用假照片，無自上傳照片時顯示（之後刪除）──
const TEST_PHOTOS = [
  'https://picsum.photos/seed/profile_a/400/500',
  'https://picsum.photos/seed/profile_b/400/500',
  'https://picsum.photos/seed/profile_c/400/500',
]

const ZODIAC_ICON: Record<string, string> = {
  '牡羊座': '♈', '金牛座': '♉', '雙子座': '♊', '巨蟹座': '♋',
  '獅子座': '♌', '處女座': '♍', '天秤座': '♎', '天蠍座': '♏',
  '射手座': '♐', '摩羯座': '♑', '水瓶座': '♒', '雙魚座': '♓',
}

const MONTH_NAMES = ['1月','2月','3月','4月','5月','6月','7月','8月','9月','10月','11月','12月']
const DOW_LABELS  = ['日','一','二','三','四','五','六']
const DEFAULT_BIRTHDAY_YEAR = 2000
const MINIMUM_AGE = 18

function getZodiac(birthday: string): string {
  const d = new Date(birthday)
  const md = (d.getMonth() + 1) * 100 + d.getDate()
  if (md >= 120 && md <= 218) return '水瓶座'
  if (md >= 219 && md <= 320) return '雙魚座'
  if (md >= 321 && md <= 419) return '牡羊座'
  if (md >= 420 && md <= 520) return '金牛座'
  if (md >= 521 && md <= 620) return '雙子座'
  if (md >= 621 && md <= 722) return '巨蟹座'
  if (md >= 723 && md <= 822) return '獅子座'
  if (md >= 823 && md <= 922) return '處女座'
  if (md >= 923 && md <= 1022) return '天秤座'
  if (md >= 1023 && md <= 1121) return '天蠍座'
  if (md >= 1122 && md <= 1221) return '射手座'
  return '摩羯座'
}

function getAge(birthday: string): number {
  const today = new Date()
  const [year, month, day] = birthday.split('-').map(Number)
  let age = today.getFullYear() - year
  const m = today.getMonth() + 1 - month
  if (m < 0 || (m === 0 && today.getDate() < day)) age--
  return age
}

function isAdultBirthday(birthday: string): boolean {
  return getAge(birthday) >= MINIMUM_AGE
}

// ── 日曆日期選擇器 ───────────────────────────────────────────────────────────
function CalendarPicker({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const currentYear = new Date().getFullYear()
  const [open, setOpen] = useState(false)
  const [showYearGrid, setShowYearGrid] = useState(false)
  const [viewYear,  setViewYear]  = useState(() => value ? +value.split('-')[0] : DEFAULT_BIRTHDAY_YEAR)
  const [viewMonth, setViewMonth] = useState(() => value ? +value.split('-')[1] - 1 : 0)
  const containerRef = useRef<HTMLDivElement>(null)

  const selParts = value ? value.split('-') : null
  const selYear  = selParts ? +selParts[0] : null
  const selMonth = selParts ? +selParts[1] - 1 : null
  const selDay   = selParts ? +selParts[2] : null

  // 點外部關閉
  useEffect(() => {
    if (!open) return
    function onDown(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false); setShowYearGrid(false)
      }
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [open])

  // value 改變時同步 view
  useEffect(() => {
    if (value) {
      const [y, m] = value.split('-')
      setViewYear(+y); setViewMonth(+m - 1)
    }
  }, [value])

  function prevMonth() {
    if (viewMonth === 0) { setViewMonth(11); setViewYear(y => y - 1) }
    else setViewMonth(m => m - 1)
  }
  function nextMonth() {
    if (viewMonth === 11) { setViewMonth(0); setViewYear(y => y + 1) }
    else setViewMonth(m => m + 1)
  }

  // 建立日曆 grid
  const firstDow = new Date(viewYear, viewMonth, 1).getDay()
  const totalDays = new Date(viewYear, viewMonth + 1, 0).getDate()
  const cells: (number | null)[] = [
    ...Array(firstDow).fill(null),
    ...Array.from({ length: totalDays }, (_, i) => i + 1),
  ]
  while (cells.length % 7 !== 0) cells.push(null)

  const yearRange = Array.from({ length: currentYear - 1950 + 1 }, (_, i) => currentYear - i)
  const displayText = selYear && selMonth !== null && selDay
    ? `${selYear}年${MONTH_NAMES[selMonth]}${selDay}日`
    : ''

  return (
    <div className="cal-picker" ref={containerRef}>
      <div
        className="cal-input"
        onClick={() => {
          if (!value && !open) {
            setViewYear(DEFAULT_BIRTHDAY_YEAR)
            setViewMonth(0)
          }
          setOpen(o => !o)
          setShowYearGrid(false)
        }}
        role="button"
        tabIndex={0}
      >
        {displayText
          ? <span>{displayText}</span>
          : <span className="cal-placeholder">選擇生日</span>
        }
        <svg className="cal-chevron" width="14" height="14" viewBox="0 0 14 14" fill="none">
          <path d="M3 5L7 9L11 5" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"/>
        </svg>
      </div>

      {open && (
        <div className="cal-popup">
          {showYearGrid ? (
            <>
              <div className="cal-header">
                <button className="cal-month-label" onClick={() => setShowYearGrid(false)}>
                  ← 選擇年份
                </button>
              </div>
              <div className="cal-year-grid">
                {yearRange.map(y => (
                  <button
                    key={y}
                    className={`cal-year-cell${y === viewYear ? ' selected' : ''}`}
                    onClick={() => { setViewYear(y); setShowYearGrid(false) }}
                  >
                    {y}
                  </button>
                ))}
              </div>
            </>
          ) : (
            <>
              <div className="cal-header">
                <button className="cal-nav" onClick={prevMonth}>‹</button>
                <button className="cal-month-label" onClick={() => setShowYearGrid(true)}>
                  {viewYear}年{MONTH_NAMES[viewMonth]}
                </button>
                <button className="cal-nav" onClick={nextMonth}>›</button>
              </div>
              <div className="cal-dow">
                {DOW_LABELS.map(d => <span key={d}>{d}</span>)}
              </div>
              <div className="cal-grid">
                {cells.map((day, i) => {
                  const isSelected = day !== null
                    && day === selDay && viewMonth === selMonth && viewYear === selYear
                  return (
                    <button
                      key={i}
                      className={`cal-day${isSelected ? ' selected' : ''}${day === null ? ' empty' : ''}`}
                      disabled={day === null}
                      onClick={() => {
                        if (!day) return
                        onChange(`${viewYear}-${String(viewMonth + 1).padStart(2,'0')}-${String(day).padStart(2,'0')}`)
                        setOpen(false)
                      }}
                    >
                      {day ?? ''}
                    </button>
                  )
                })}
              </div>
            </>
          )}
        </div>
      )}
    </div>
  )
}

async function fileToResizedBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const img = new Image()
    const url = URL.createObjectURL(file)
    img.onload = () => {
      URL.revokeObjectURL(url)
      const maxSize = 600
      const scale = Math.min(1, maxSize / Math.max(img.width, img.height))
      const w = Math.round(img.width * scale)
      const h = Math.round(img.height * scale)
      const canvas = document.createElement('canvas')
      canvas.width = w
      canvas.height = h
      canvas.getContext('2d')!.drawImage(img, 0, 0, w, h)
      resolve(canvas.toDataURL('image/jpeg', 0.82))
    }
    img.onerror = reject
    img.src = url
  })
}

function ProfileCardFullscreen({
  user, bio, birthday, interestTags, photos, onClose, onGoAddPhoto,
}: {
  user: DemoUser; bio: string; birthday: string
  interestTags: string[]; photos: string[]
  onClose: () => void; onGoAddPhoto: () => void
}) {
  const [photoIndex, setPhotoIndex] = useState(0)
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    requestAnimationFrame(() => setVisible(true))
    function onPopState() { handleClose() }
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  function handleClose() { setVisible(false); setTimeout(onClose, 250) }

  function handlePhotoTap(e: React.MouseEvent<HTMLDivElement>) {
    if (photos.length <= 1) return
    const rect = e.currentTarget.getBoundingClientRect()
    if (e.clientX - rect.left < rect.width / 2) setPhotoIndex(i => Math.max(0, i - 1))
    else setPhotoIndex(i => Math.min(photos.length - 1, i + 1))
  }

  return (
    <div className={`profile-fs-overlay${visible ? ' visible' : ''}`}>
      <div className="profile-fs-topbar">
        <div className="profile-fs-topbar-left">
          <span>預覽模式</span>
        </div>
        <button className="profile-fs-close" onClick={handleClose} aria-label="關閉預覽">
          <EyeOff size={18} strokeWidth={1.8} />
        </button>
      </div>

      <div className="profile-fs-body">
        <div className="profile-fs-card">
          <div className="profile-fs-photo-area" onClick={handlePhotoTap}>
            {photos.length > 0 ? (
              <img src={photos[photoIndex]} alt={user.nickname} className="profile-fs-photo" />
            ) : (
              <div className="profile-fs-no-photo">
                <Camera size={34} color="#c89a72" strokeWidth={1.5} />
                <p className="profile-fs-no-photo-text">加一張照片，讓別人更容易認識你</p>
                <button
                  className="profile-fs-add-photo-btn"
                  onClick={e => { e.stopPropagation(); handleClose(); onGoAddPhoto() }}
                >去新增</button>
              </div>
            )}
            {photos.length > 1 && (
              <div className="swipe-photo-dots">
                {photos.map((_, i) => (
                  <span key={i} className={`swipe-photo-dot${i === photoIndex ? ' active' : ''}`} />
                ))}
              </div>
            )}
          </div>

          <div className="profile-fs-info">
            <div className="swipe-name-row">
              <h2 className="swipe-name">{user.nickname}</h2>
              {birthday && (
                <>
                  <span className="swipe-age">{getAge(birthday)}</span>
                  <span className="swipe-zodiac">
                    <span className="swipe-zodiac-icon">{ZODIAC_ICON[getZodiac(birthday)] ?? '✦'}</span>
                    {getZodiac(birthday)}
                  </span>
                </>
              )}
            </div>
            {bio
              ? <p className="swipe-bio">{bio}</p>
              : <p className="swipe-bio profile-fs-muted">還沒填自我介紹</p>
            }
            <div className="swipe-divider" />
            <div className="swipe-resonance-strip">
              <span className="swipe-resonance-dot" />
              <span>你今天的共鳴語句，會顯示在這裡</span>
            </div>
            {interestTags.length > 0 && (
              <div className="swipe-tags" style={{ marginTop: 10 }}>
                {interestTags.map(tag => <span key={tag} className="swipe-tag">{tag}</span>)}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

export default function ProfilePage({ user, setUser }: Props) {
  const [bio, setBio] = useState('')
  const [birthday, setBirthday] = useState('')
  const [interestTags, setInterestTags] = useState<string[]>([])
  const [photos, setPhotos] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [showPreview, setShowPreview] = useState(false)
  const [toast, setToast] = useState('')
  const toastTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const { preference, isAnimating, setPreference } = useTheme()

  useEffect(() => {
    getProfile(user.userId)
      .then(res => {
        setBio(res.data.bio ?? '')
        setBirthday(res.data.birthday ?? '')
        setInterestTags(res.data.interestTags ?? [])
        const loaded = res.data.photoDataUrls ?? []
        setPhotos(loaded.length > 0 ? loaded : TEST_PHOTOS)
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [user.userId])

  function showToast(msg: string) {
    setToast(msg)
    if (toastTimer.current) clearTimeout(toastTimer.current)
    toastTimer.current = setTimeout(() => setToast(''), 2000)
  }

  function toggleTag(tag: string) {
    setInterestTags(prev => prev.includes(tag) ? prev.filter(t => t !== tag) : [...prev, tag])
  }

  async function handleAddPhotos(files: FileList) {
    const remaining = MAX_PHOTOS - photos.length
    if (remaining <= 0) return
    const picked = Array.from(files).slice(0, remaining)
    const results = await Promise.all(picked.map(fileToResizedBase64))
    setPhotos(prev => [...prev, ...results])
  }

  function removePhoto(index: number) {
    setPhotos(prev => prev.filter((_, i) => i !== index))
  }

  async function save() {
    if (birthday && !isAdultBirthday(birthday)) {
      showToast('未滿 18 歲暫時不能使用同頻 Today。')
      return
    }

    setSaving(true)
    try {
      await updateProfile(user.userId, { bio, birthday: birthday || undefined, interestTags, photoDataUrls: photos })
      showToast('已儲存 ✓')
    } catch (err) {
      showToast(err instanceof Error ? err.message : '儲存失敗')
    }
    setSaving(false)
  }

  function logout() {
    clearAuthToken()
    localStorage.removeItem('same-day-demo-user')
    sessionStorage.removeItem('same-day-demo-user')
    setUser(null)
  }

  return (
    <div className="page profile-page">
      <header className="page-header">
        <p className="eyebrow">我的</p>
        <h1>{user.nickname}</h1>
      </header>

      {/* Photos */}
      <section className="panel profile-photos-panel">
        <div className="profile-section-header">
          <p className="setting-section-title" style={{ marginBottom: 0 }}>自拍照片</p>
          <button
            className={`profile-eye-btn${showPreview ? ' active' : ''}`}
            type="button"
            onClick={() => setShowPreview(true)}
            aria-label="預覽我的卡"
          >
            <Eye size={18} strokeWidth={1.8} />
          </button>
        </div>
        <div className="profile-photo-grid" style={{ marginTop: 12 }}>
          {photos.map((src, i) => (
            <div key={i} className="profile-photo-cell">
              <img src={src} alt={`照片 ${i + 1}`} />
              <button
                className="profile-photo-remove"
                type="button"
                onClick={() => removePhoto(i)}
                aria-label="移除照片"
              >
                <X size={14} strokeWidth={2.5} />
              </button>
            </div>
          ))}
          {photos.length < MAX_PHOTOS && (
            <button
              className="profile-photo-add"
              type="button"
              onClick={() => fileInputRef.current?.click()}
              aria-label="新增照片"
            >
              <Plus size={24} strokeWidth={1.8} />
            </button>
          )}
        </div>
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          multiple
          style={{ display: 'none' }}
          onChange={e => { if (e.target.files) handleAddPhotos(e.target.files) }}
        />
      </section>

      {/* Birthday */}
      <section className="panel">
        <p className="setting-section-title">生日</p>
        <CalendarPicker value={birthday} onChange={setBirthday} />
        {birthday && (
          <span className="profile-zodiac-badge">
            {ZODIAC_ICON[getZodiac(birthday)]} {getZodiac(birthday)}・{getAge(birthday)} 歲
          </span>
        )}
      </section>

      {/* Bio */}
      <section className="panel">
        <p className="setting-section-title">自我介紹</p>
        {loading ? (
          <div className="profile-loading-bar" />
        ) : (
          <textarea
            className="profile-bio-input"
            rows={4}
            maxLength={150}
            placeholder="說說你是什麼樣的人……"
            value={bio}
            onChange={e => setBio(e.target.value)}
          />
        )}
        <span className="profile-bio-count">{bio.length}/150</span>
      </section>

      {/* Interest tags */}
      <section className="panel">
        <p className="setting-section-title">興趣標籤</p>
        <div className="ob-tags">
          {INTEREST_OPTIONS.map(tag => (
            <button
              key={tag}
              type="button"
              className={`ob-tag${interestTags.includes(tag) ? ' selected' : ''}`}
              onClick={() => toggleTag(tag)}
            >
              {tag}
            </button>
          ))}
        </div>
      </section>

      {/* Appearance */}
      <section className="panel">
        <p className="setting-section-title">外觀</p>
        <div className="theme-segmented" role="radiogroup" aria-label="外觀主題" aria-disabled={isAnimating}>
          {PREFS.map(({ value, icon, label }) => (
            <button
              key={value}
              role="radio"
              aria-checked={preference === value}
              className={`theme-seg-option${preference === value ? ' selected' : ''}`}
              onClick={() => setPreference(value)}
              disabled={isAnimating}
            >
              {icon === 'sun'  && <Sun  size={18} strokeWidth={1.8} />}
              {icon === 'moon' && <Moon size={18} strokeWidth={1.8} />}
              {label && <span className="theme-seg-label">{label}</span>}
            </button>
          ))}
        </div>
      </section>

      {/* Save + Logout */}
      <section className="panel profile-actions-panel">
        <button
          className="profile-save-btn"
          type="button"
          disabled={saving || loading}
          onClick={save}
        >
          {saving ? '儲存中⋯' : '儲存變更'}
        </button>
        <button className="profile-logout-btn" type="button" onClick={logout}>
          登出
        </button>
      </section>

      {toast && <div className="center-toast">{toast}</div>}

      {showPreview && (
        <ProfileCardFullscreen
          user={user}
          bio={bio}
          birthday={birthday}
          interestTags={interestTags}
          photos={photos}
          onClose={() => setShowPreview(false)}
          onGoAddPhoto={() => setTimeout(() => fileInputRef.current?.click(), 300)}
        />
      )}
    </div>
  )
}
