import { useState, useEffect, useRef } from 'react'
import { Sun, Moon, Plus, X } from 'lucide-react'
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

function ProfileCardPreview({
  user,
  bio,
  interestTags,
  photos,
  onClose,
}: {
  user: DemoUser
  bio: string
  interestTags: string[]
  photos: string[]
  onClose: () => void
}) {
  const [photoIndex, setPhotoIndex] = useState(0)

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
    <div className="profile-preview-overlay" onClick={onClose}>
      <div className="profile-preview-modal" onClick={e => e.stopPropagation()}>
        <div className="profile-preview-topbar">
          <span className="profile-preview-hint">別人看到你的卡會長這樣</span>
          <button className="profile-preview-close" onClick={onClose} aria-label="關閉">
            <X size={18} strokeWidth={2} />
          </button>
        </div>

        <div className="profile-preview-card">
          {/* Photo area */}
          <div className="profile-preview-photo-wrap" onClick={handlePhotoTap}>
            {photos.length > 0 ? (
              <img src={photos[photoIndex]} alt={user.nickname} className="profile-preview-photo" />
            ) : (
              <div className="profile-preview-no-photo">
                <span className="profile-preview-initial">{user.nickname[0]}</span>
                <p className="profile-preview-no-photo-hint">還沒有照片，<br />在上方新增自拍吧</p>
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

          {/* Info area */}
          <div className="profile-preview-info">
            <div className="swipe-name-row">
              <h2 className="swipe-name">{user.nickname}</h2>
            </div>

            {bio ? (
              <p className="swipe-bio">{bio}</p>
            ) : (
              <p className="swipe-bio" style={{ opacity: 0.4 }}>還沒填自我介紹</p>
            )}

            {interestTags.length > 0 && (
              <div className="swipe-tags" style={{ marginTop: 10 }}>
                {interestTags.map(tag => (
                  <span key={tag} className="swipe-tag">{tag}</span>
                ))}
              </div>
            )}

            <div className="profile-preview-today-placeholder">
              <span className="swipe-today-label">今天想說的話</span>
              <p className="profile-preview-today-text">
                「你今天送出的心情故事，會顯示在這裡…」
              </p>
            </div>
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
        setPhotos(res.data.photoDataUrls ?? [])
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

      {/* Card preview modal */}
      {showPreview && (
        <ProfileCardPreview
          user={user}
          bio={bio}
          interestTags={interestTags}
          photos={photos}
          onClose={() => setShowPreview(false)}
        />
      )}
    </div>
  )
}
