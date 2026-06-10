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

export default function ProfilePage({ user, setUser }: Props) {
  const [bio, setBio] = useState('')
  const [interestTags, setInterestTags] = useState<string[]>([])
  const [photos, setPhotos] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [saveMsg, setSaveMsg] = useState('')
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
    setSaveMsg('')
    try {
      await updateProfile(user.userId, { bio, interestTags, photoDataUrls: photos })
      setSaveMsg('已儲存')
      setTimeout(() => setSaveMsg(''), 2000)
    } catch (err) {
      setSaveMsg(err instanceof Error ? err.message : '儲存失敗')
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
        {saveMsg && (
          <p className={`profile-save-msg${saveMsg === '已儲存' ? ' ok' : ' err'}`}>
            {saveMsg}
          </p>
        )}
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
    </div>
  )
}
