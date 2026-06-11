import { useState, useEffect, useRef } from 'react'
import { Sun, Moon, Plus, X, Eye, Camera } from 'lucide-react'
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
  user,
  bio,
  interestTags,
  photos,
  onClose,
  onGoAddPhoto,
}: {
  user: DemoUser
  bio: string
  interestTags: string[]
  photos: string[]
  onClose: () => void
  onGoAddPhoto: () => void
}) {
  const [photoIndex, setPhotoIndex] = useState(0)
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    requestAnimationFrame(() => setVisible(true))
    function onPopState() { handleClose() }
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  function handleClose() {
    setVisible(false)
    setTimeout(onClose, 250)
  }

  function handlePhotoTap(e: React.MouseEvent<HTMLDivElement>) {
    if (photos.length <= 1) return
    const rect = e.currentTarget.getBoundingClientRect()
    if (e.clientX - rect.left < rect.width / 2) {
      setPhotoIndex(i => Math.max(0, i - 1))
    } else {
      setPhotoIndex(i => Math.min(photos.length - 1, i + 1))
    }
  }

  return (
    <div className={`profile-fs-overlay${visible ? ' visible' : ''}`}>
      {/* 頂部提示列 */}
      <div className="profile-fs-topbar">
        <div className="profile-fs-topbar-left">
          <Eye size={15} strokeWidth={2} />
          <span>預覽模式</span>
        </div>
        <button className="profile-fs-close" onClick={handleClose} aria-label="關閉">
          <X size={16} strokeWidth={2} />
        </button>
      </div>

      {/* 可捲動的卡片區 */}
      <div className="profile-fs-body">
        <div className="profile-fs-card">
          {/* 照片區 */}
          <div className="profile-fs-photo-area" onClick={handlePhotoTap}>
            {photos.length > 0 ? (
              <img
                src={photos[photoIndex]}
                alt={user.nickname}
                className="profile-fs-photo"
              />
            ) : (
              <div className="profile-fs-no-photo">
                <Camera size={34} color="#c89a72" strokeWidth={1.5} />
                <p className="profile-fs-no-photo-text">加一張照片，讓別人更容易認識你</p>
                <button
                  className="profile-fs-add-photo-btn"
                  onClick={e => { e.stopPropagation(); handleClose(); onGoAddPhoto() }}
                >
                  去新增
                </button>
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

          {/* 文字資訊 */}
          <div className="profile-fs-info">
            <div className="swipe-name-row">
              <h2 className="swipe-name">{user.nickname}</h2>
            </div>

            {bio ? (
              <p className="swipe-bio">{bio}</p>
            ) : (
              <p className="swipe-bio profile-fs-muted">還沒填自我介紹</p>
            )}

            <div className="swipe-divider" />

            <div className="swipe-resonance-strip">
              <span className="swipe-resonance-dot" />
              <span>你今天的共鳴語句，會顯示在這裡</span>
            </div>

            <p className="swipe-today-label">今天想說的話</p>
            <p className="swipe-today-text">「你今天送出的心情故事，會顯示在這裡…」</p>

            {interestTags.length > 0 && (
              <div className="swipe-tags" style={{ marginTop: 10 }}>
                {interestTags.map(tag => (
                  <span key={tag} className="swipe-tag">{tag}</span>
                ))}
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
    setInterestTags(prev =>
      prev.includes(tag) ? prev.filter(t => t !== tag) : [...prev, tag]
    )
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
    setSaving(true)
    try {
      await updateProfile(user.userId, { bio, interestTags, photoDataUrls: photos })
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
        <p className="setting-section-title">自拍照片</p>
        <div className="profile-photo-grid">
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
        <button
          className="profile-preview-btn"
          type="button"
          onClick={() => setShowPreview(true)}
        >
          預覽我的卡
        </button>
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

      {/* Center toast */}
      {toast && <div className="center-toast">{toast}</div>}

      {/* 全螢幕卡片預覽 */}
      {showPreview && (
        <ProfileCardFullscreen
          user={user}
          bio={bio}
          interestTags={interestTags}
          photos={photos}
          onClose={() => setShowPreview(false)}
          onGoAddPhoto={() => setTimeout(() => fileInputRef.current?.click(), 300)}
        />
      )}
    </div>
  )
}
