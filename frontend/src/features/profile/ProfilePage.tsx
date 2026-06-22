import { useState, useEffect, useRef } from "react"
import { useNavigate } from "react-router-dom"
import { Sun, Moon, Plus, X, Eye, Check, ChevronRight } from "lucide-react"
import { useAuth } from "../auth/AuthContext"
import { useTheme, type ThemePreference } from "../../shared/theme/ThemeContext"
import { getProfile, updateProfile } from "./api"
import { CalendarPicker } from "./CalendarPicker"
import { ProfileCardFullscreen } from "./ProfileCardFullscreen"
import { getZodiac, getAge, isAdultBirthday, fileToResizedBase64, ZODIAC_ICON } from "./profileUtils"
import {
  GENDER_OPTIONS, RELATIONSHIP_OPTIONS, PERSONALITY_OPTIONS, APPEARANCE_OPTIONS, BLOOD_OPTIONS,
} from "./profileFields"

const INTEREST_OPTIONS = [
  "咖啡", "散步", "電影", "音樂", "閱讀", "料理",
  "旅遊", "攝影", "運動", "遊戲", "動漫", "Podcast",
  "貓狗", "手作", "冥想", "追劇", "寫作", "展覽",
]

// 欄位小標：標題 + 必填*/選填 + ·單選/·可多選 提示
function FieldLabel({ title, required, optional, mode }: {
  title: string; required?: boolean; optional?: boolean; mode?: 'single' | 'multi'
}) {
  return (
    <p className="setting-section-title profile-field-label">
      {title}
      {required && <span className="profile-required"> *</span>}
      {optional && <span className="profile-optional"> 選填</span>}
      {mode === 'single' && <span className="profile-hint"> · 單選</span>}
      {mode === 'multi' && <span className="profile-hint"> · 可多選</span>}
    </p>
  )
}

// 單選 chip 群（再點一次可取消，性別在存檔時驗證必填）
function SingleChips({ options, value, onChange, allowClear = true }: {
  options: readonly string[]; value: string; onChange: (v: string) => void; allowClear?: boolean
}) {
  return (
    <div className="ob-tags">
      {options.map(o => (
        <button
          key={o}
          type="button"
          className={`ob-tag${value === o ? ' selected' : ''}`}
          onClick={() => onChange(allowClear && value === o ? '' : o)}
        >{o}</button>
      ))}
    </div>
  )
}

// 多選 chip 群 + 可自訂標籤（預設選項 + 尾端「+」內聯新增）
function TagPicker({ options, values, onToggle, maxLen = 8 }: {
  options: readonly string[]; values: string[]; onToggle: (v: string) => void; maxLen?: number
}) {
  const [adding, setAdding] = useState(false)
  const [draft, setDraft] = useState('')
  const committedRef = useRef(false)   // 防呆：Enter 與 blur 不重複觸發
  const customTags = values.filter(v => !options.includes(v))

  function startAdd() { setDraft(''); committedRef.current = false; setAdding(true) }
  function commit() {
    if (committedRef.current) return
    committedRef.current = true
    const t = draft.trim()
    if (t && !values.includes(t)) onToggle(t)   // 重複名稱不重加
    setDraft('')
    setAdding(false)
  }
  function cancel() { setDraft(''); setAdding(false) }

  return (
    <div className="ob-tags">
      {options.map(o => (
        <button
          key={o}
          type="button"
          className={`ob-tag${values.includes(o) ? ' selected' : ''}`}
          onClick={() => onToggle(o)}
        >
          {values.includes(o) && <Check size={13} strokeWidth={3} className="ob-tag-check" />}{o}
        </button>
      ))}
      {customTags.map(t => (
        <button key={t} type="button" className="ob-tag selected" onClick={() => onToggle(t)}>
          <Check size={13} strokeWidth={3} className="ob-tag-check" />{t}
        </button>
      ))}
      {adding ? (
        <input
          className="ob-tag-input"
          autoFocus
          maxLength={maxLen}
          placeholder="輸入後按 Enter"
          value={draft}
          onChange={e => setDraft(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter') { e.preventDefault(); commit() }
            else if (e.key === 'Escape') { cancel() }
          }}
          onBlur={() => { if (draft.trim()) commit(); else cancel() }}
        />
      ) : (
        <button type="button" className="ob-tag ob-tag-add" onClick={startAdd} aria-label="新增標籤">
          <Plus size={16} strokeWidth={2.2} />
        </button>
      )}
    </div>
  )
}

function CompactTagPicker({ options, values, onToggle, editing }: {
  options: readonly string[]; values: string[]; onToggle: (v: string) => void; editing: boolean
}) {
  return (
    <div className={`profile-compact-tags${editing ? ' editing' : ''}`}>
      {editing ? (
        <div className="profile-compact-tag-editor">
          <TagPicker options={options} values={values} onToggle={onToggle} />
        </div>
      ) : (
        <div className="profile-compact-tag-bar">
          <div className="profile-selected-tags" aria-label="已選標籤">
            {values.length > 0 ? (
              values.map(tag => (
                <span key={tag} className="profile-selected-tag">
                  <Check size={12} strokeWidth={3} />{tag}
                </span>
              ))
            ) : (
              <span className="profile-tag-empty">還沒選擇</span>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

const PREFS: { value: ThemePreference; icon?: "sun" | "moon"; label?: string }[] = [
  { value: "day",   icon: "sun" },
  { value: "night", icon: "moon" },
  { value: "auto",  label: "自動" },
]

const MAX_PHOTOS = 6

// 測試用假照片，無自上傳照片時顯示（之後刪除）
const TEST_PHOTOS = [
  "https://picsum.photos/seed/profile_a/400/500",
  "https://picsum.photos/seed/profile_b/400/500",
  "https://picsum.photos/seed/profile_c/400/500",
]

export default function ProfilePage() {
  const navigate = useNavigate()
  const { user, logout } = useAuth()
  const [bio, setBio] = useState('')
  const [birthday, setBirthday] = useState('')
  const [interestTags, setInterestTags] = useState<string[]>([])
  const [nickname, setNickname] = useState('')
  const [gender, setGender] = useState('')
  const [relationship, setRelationship] = useState('')
  const [personalityTags, setPersonalityTags] = useState<string[]>([])
  const [appearanceTags, setAppearanceTags] = useState<string[]>([])
  const [height, setHeight] = useState('')
  const [weight, setWeight] = useState('')
  const [occupation, setOccupation] = useState('')
  const [school, setSchool] = useState('')
  const [bloodType, setBloodType] = useState('')
  const [photos, setPhotos] = useState<string[]>([])
  const [photosDirty, setPhotosDirty] = useState(false)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [showPreview, setShowPreview] = useState(false)
  const [editingTags, setEditingTags] = useState(false)
  const [toast, setToast] = useState('')
  const toastTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const dragIndex = useRef<number | null>(null)
  const { preference, isAnimating, setPreference } = useTheme()

  // 預覽中再點底部「我的」→ 關閉預覽，回到自我介紹編輯頁
  useEffect(() => {
    function onReselect() { setShowPreview(false) }
    window.addEventListener('nav-reselect-profile', onReselect)
    return () => window.removeEventListener('nav-reselect-profile', onReselect)
  }, [])

  useEffect(() => {
    if (!user) return
    getProfile(user.userId)
      .then(res => {
        setBio(res.data.bio ?? '')
        setBirthday(res.data.birthday ?? '')
        setInterestTags(res.data.interestTags ?? [])
        setNickname(res.data.nickname ?? user.nickname ?? '')
        setGender(res.data.gender ?? '')
        setRelationship(res.data.relationship ?? '')
        setPersonalityTags(res.data.personalityTags ?? [])
        setAppearanceTags(res.data.appearanceTags ?? [])
        setHeight(res.data.height != null ? String(res.data.height) : '')
        setWeight(res.data.weight != null ? String(res.data.weight) : '')
        setOccupation(res.data.occupation ?? '')
        setSchool(res.data.school ?? '')
        setBloodType(res.data.bloodType ?? '')
        const loaded = res.data.photoDataUrls ?? []
        setPhotos(loaded.length > 0 ? loaded : TEST_PHOTOS)
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [user?.userId])

  function showToast(msg: string) {
    setToast(msg)
    if (toastTimer.current) clearTimeout(toastTimer.current)
    toastTimer.current = setTimeout(() => setToast(''), 2000)
  }

  function toggleTag(tag: string) {
    setInterestTags(prev => prev.includes(tag) ? prev.filter(t => t !== tag) : [...prev, tag])
  }

  function togglePersonality(tag: string) {
    setPersonalityTags(prev => prev.includes(tag) ? prev.filter(t => t !== tag) : [...prev, tag])
  }

  function toggleAppearance(tag: string) {
    setAppearanceTags(prev => prev.includes(tag) ? prev.filter(t => t !== tag) : [...prev, tag])
  }

  async function handleAddPhotos(files: FileList) {
    const remaining = MAX_PHOTOS - photos.length
    if (remaining <= 0) return
    const picked = Array.from(files).slice(0, remaining)
    const results = await Promise.all(picked.map(fileToResizedBase64))
    setPhotos(prev => [...prev, ...results])
    setPhotosDirty(true)
  }

  function removePhoto(index: number) {
    setPhotos(prev => prev.filter((_, i) => i !== index))
    setPhotosDirty(true)
  }

  // 拖曳排序（第一張＝個人卡主照片）
  function dropPhoto(toIndex: number) {
    const from = dragIndex.current
    dragIndex.current = null
    if (from == null || from === toIndex) return
    setPhotos(prev => {
      const next = [...prev]
      const [moved] = next.splice(from, 1)
      next.splice(toIndex, 0, moved)
      return next
    })
    setPhotosDirty(true)
  }

  async function save() {
    if (!gender) {
      showToast('請選擇生理性別（必填）。')
      return
    }
    if (birthday && !isAdultBirthday(birthday)) {
      showToast('未滿 18 歲暫時不能使用同頻 Today。')
      return
    }

    if (!user) return
    setSaving(true)
    try {
      await updateProfile(user.userId, {
        nickname: nickname.trim() || undefined,
        bio,
        birthday: birthday || undefined,
        interestTags,
        gender,
        relationship: relationship || undefined,
        personalityTags,
        appearanceTags,
        height: height ? Number(height) : null,
        weight: weight ? Number(weight) : null,
        occupation: occupation.trim() || undefined,
        school: school.trim() || undefined,
        bloodType: bloodType || undefined,
        ...(photosDirty ? { photoDataUrls: photos } : {}),
      })
      setEditingTags(false)
      showToast('已儲存 ✓')
    } catch (err) {
      showToast(err instanceof Error ? err.message : '儲存失敗')
    }
    setSaving(false)
  }

  if (!user) return null

  return (
    <div className="page profile-page">
      <header className="page-header">
        <h1>{user.nickname}</h1>
      </header>

      {/* Photos */}
      <section className="panel profile-photos-panel">
        <div className="profile-section-header">
          <p className="setting-section-title profile-field-label" style={{ marginBottom: 0 }}>
            照片 <span className="profile-hint">· 第一張為主照片，可拖曳排序</span>
          </p>
          <div className="profile-photo-actions">
            <button
              className={`profile-photo-edit-btn${editingTags ? ' active' : ''}`}
              type="button"
              onClick={() => setEditingTags(value => !value)}
            >
              {editingTags ? '完成' : '編輯標籤'}
            </button>
            <button
              className={`profile-eye-btn${showPreview ? ' active' : ''}`}
              type="button"
              onClick={() => setShowPreview(true)}
              aria-label="預覽我的卡"
            >
              <Eye size={26} strokeWidth={2} />
            </button>
          </div>
        </div>
        <div className="profile-photo-grid" style={{ marginTop: 12 }}>
          {photos.map((src, i) => (
            <div
              key={i}
              className="profile-photo-cell"
              draggable
              onDragStart={() => { dragIndex.current = i }}
              onDragOver={e => e.preventDefault()}
              onDrop={() => dropPhoto(i)}
            >
              <img src={src} alt={`照片 ${i + 1}`} />
              {i === 0 && <span className="profile-photo-main">主照片</span>}
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

      {/* 暱稱 */}
      <section className="panel">
        <FieldLabel title="暱稱" required />
        <input
          className="profile-text-input"
          maxLength={20}
          placeholder="想被怎麼稱呼？"
          value={nickname}
          onChange={e => setNickname(e.target.value)}
        />
      </section>

      {/* 生理性別（必填）*/}
      <section className="panel">
        <FieldLabel title="生理性別" required mode="single" />
        <SingleChips options={GENDER_OPTIONS} value={gender} onChange={setGender} allowClear={false} />
      </section>

      {/* Birthday */}
      <section className="panel">
        <FieldLabel title="生日" optional />
        <CalendarPicker value={birthday} onChange={setBirthday} />
        {birthday && (
          <span className="profile-zodiac-badge">
            {ZODIAC_ICON[getZodiac(birthday)]} {getZodiac(birthday)}・{getAge(birthday)} 歲
          </span>
        )}
      </section>

      {/* Bio */}
      <section className="panel">
        <FieldLabel title="自我介紹" optional />
        {loading ? (
          <div className="profile-loading-bar" />
        ) : (
          <textarea
            className="profile-bio-input"
            rows={4}
            maxLength={300}
            placeholder="說說你是什麼樣的人……"
            value={bio}
            onChange={e => setBio(e.target.value)}
          />
        )}
        <span className="profile-bio-count">{bio.length}/300</span>
      </section>

      {/* Interest tags */}
      <section className="panel">
        <FieldLabel title="興趣標籤" optional mode="multi" />
        <CompactTagPicker
          options={INTEREST_OPTIONS}
          values={interestTags}
          onToggle={toggleTag}
          editing={editingTags}
        />
      </section>

      {/* 個性 */}
      <section className="panel">
        <FieldLabel title="個性" optional mode="multi" />
        <CompactTagPicker
          options={PERSONALITY_OPTIONS}
          values={personalityTags}
          onToggle={togglePersonality}
          editing={editingTags}
        />
      </section>

      {/* 外貌 */}
      <section className="panel">
        <FieldLabel title="外貌" optional mode="multi" />
        <CompactTagPicker
          options={APPEARANCE_OPTIONS}
          values={appearanceTags}
          onToggle={toggleAppearance}
          editing={editingTags}
        />
      </section>

      {/* 感情狀態 */}
      <section className="panel">
        <FieldLabel title="感情狀態" optional mode="single" />
        <SingleChips options={RELATIONSHIP_OPTIONS} value={relationship} onChange={setRelationship} />
      </section>

      {/* 身高 / 體重 */}
      <section className="panel">
        <FieldLabel title="身高 / 體重" optional />
        <div className="profile-field-row">
          <div className="profile-field-unit">
            <input
              className="profile-text-input"
              type="number" inputMode="numeric" min={100} max={250}
              placeholder="身高"
              value={height}
              onChange={e => setHeight(e.target.value)}
            />
            <span className="profile-unit">cm</span>
          </div>
          <div className="profile-field-unit">
            <input
              className="profile-text-input"
              type="number" inputMode="numeric" min={30} max={300}
              placeholder="體重"
              value={weight}
              onChange={e => setWeight(e.target.value)}
            />
            <span className="profile-unit">kg</span>
          </div>
        </div>
      </section>

      {/* 職業 / 學校 */}
      <section className="panel">
        <FieldLabel title="職業 / 學校" optional />
        <input
          className="profile-text-input"
          maxLength={30}
          placeholder="職業"
          value={occupation}
          onChange={e => setOccupation(e.target.value)}
          style={{ marginBottom: 8 }}
        />
        <input
          className="profile-text-input"
          maxLength={30}
          placeholder="學校"
          value={school}
          onChange={e => setSchool(e.target.value)}
        />
      </section>

      {/* 血型 */}
      <section className="panel">
        <FieldLabel title="血型" optional mode="single" />
        <SingleChips options={BLOOD_OPTIONS} value={bloodType} onChange={setBloodType} />
      </section>

      {/* ── 設定（與個人資料分開）── */}
      <p className="profile-settings-divider">設定</p>

      {/* 外觀 */}
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

      {/* 帳號中心 */}
      <section className="panel">
        <button className="profile-nav-row" type="button" onClick={() => navigate('/account')}>
          <span>帳號中心</span>
          <ChevronRight size={18} strokeWidth={1.8} />
        </button>
      </section>

      {/* 登出 */}
      <section className="panel">
        <button className="profile-logout-btn" type="button" onClick={logout}>
          登出
        </button>
      </section>

      {/* 儲存：吸附底部 */}
      <div className="profile-save-bar">
        <button
          className="profile-save-btn"
          type="button"
          disabled={saving || loading}
          onClick={save}
        >
          {saving ? '儲存中⋯' : '儲存變更'}
        </button>
      </div>

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
