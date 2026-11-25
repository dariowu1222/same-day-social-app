import { useState, useEffect, useRef } from "react"
import { Plus, X, Check, LocateFixed, Bell, Settings, ChevronRight } from "lucide-react"
import { useNavigate } from "react-router-dom"
import { useAuth } from "../auth/AuthContext"
import { getProfile, updateProfile } from "./api"
import { CalendarPicker } from "./CalendarPicker"
import { ProfileCardFullscreen } from "./ProfileCardFullscreen"
import { getZodiac, getAge, isAdultBirthday, fileToResizedBase64, ZODIAC_ICON } from "./profileUtils"
import { uploadMedia, uploadErrorMessage } from "../../shared/api/media"
import AgeRangeSlider, { AGE_FLOOR, AGE_CEIL } from "../../shared/ui/AgeRangeSlider"
import {
  GENDER_OPTIONS, RELATIONSHIP_OPTIONS, DATING_GOAL_OPTIONS, LOOKING_FOR_OPTIONS,
  PERSONALITY_OPTIONS, APPEARANCE_OPTIONS, BLOOD_OPTIONS,
  AREA_OPTIONS, DISTANCE_MODE_LABELS, DISTANCE_KM_MIN, DISTANCE_KM_MAX, type DistanceMode,
} from "./profileFields"
import { locateMyArea } from "../../shared/geo/locate"
import { locateFailureCopy } from "./locationCopy"

const INTEREST_OPTIONS = [
  "咖啡", "散步", "電影", "音樂", "閱讀", "料理",
  "旅遊", "攝影", "運動", "遊戲", "動漫", "Podcast",
  "貓狗", "手作", "冥想", "追劇", "寫作", "展覽",
]

const ACTIVE_TIME_OPTIONS = ['早上', '下午', '晚上', '深夜'] as const
const LANGUAGE_OPTIONS = ['中文', '英文', '台語', '日文', '韓文'] as const
const DISTANCE_MODE_OPTIONS = [DISTANCE_MODE_LABELS.km, DISTANCE_MODE_LABELS.area] as const

/**
 * 距離輸入框的值 → 要送給後端的數字。
 * 留白、不是數字、或小於等於 0 → 0（代表清掉這個偏好）。
 * 其餘四捨五入並收進後端接受的範圍：後端欄位是整數，送小數會整包被擋下來，
 * 而且使用者只會看到「儲存失敗」，不會知道是哪一格。
 */
function clampDistance(value: string) {
  const parsed = Number(value)
  if (!value || !Number.isFinite(parsed) || parsed <= 0) return 0
  return Math.min(DISTANCE_KM_MAX, Math.max(DISTANCE_KM_MIN, Math.round(parsed)))
}

// 欄位小標：只顯示標題 +（必填時）*；optional/mode 仍保留於型別供呼叫端沿用，但不再顯示提示文字
function FieldLabel({ title, required }: {
  title: string; required?: boolean; optional?: boolean; mode?: 'single' | 'multi'
}) {
  return (
    <p className="setting-section-title profile-field-label">
      {title}
      {required && <span className="profile-required"> *</span>}
    </p>
  )
}

// 單選 chip 群（再點一次可取消，性別在存檔時驗證必填）
function SingleChips({ options, value, onChange, allowClear = true, disabled = false }: {
  options: readonly string[]; value: string; onChange: (v: string) => void; allowClear?: boolean; disabled?: boolean
}) {
  return (
    <div className="ob-tags">
      {options.map(o => (
        <button
          key={o}
          type="button"
          disabled={disabled}
          className={`ob-tag${value === o ? ' selected' : ''}`}
          onClick={() => { if (!disabled) onChange(allowClear && value === o ? '' : o) }}
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
          placeholder="輸入後點空白處完成"
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

const MAX_PHOTOS = 6

type Section = null | 'photos' | 'basic' | 'about' | 'prefs'

const SECTION_TITLE: Record<Exclude<Section, null>, string> = {
  photos: '照片',
  basic: '基本資料',
  about: '關於我',
  prefs: '配對偏好',
}

// 註冊時存的是 MALE／FEMALE／PRIVATE，這一頁的選項是「男」「女」。
// 這裡只負責顯示時對得起來，不改存的值——兩邊的值要不要統一是另一件事。
function genderLabel(value: string) {
  if (value === 'MALE') return '男'
  if (value === 'FEMALE') return '女'
  return value
}

/** 沒放照片時代表自己的那顆圓（珊瑚色＝我），跟聊天室裡對方的綠色那顆是一對。 */
function MeAvatar() {
  return (
    <svg viewBox="0 0 40 40" aria-hidden="true">
      <rect width="40" height="40" fill="#fbe3d8" />
      <circle cx="20" cy="22" r="13" fill="#c9502f" />
      <ellipse cx="17.4" cy="20" rx="1.6" ry="2.2" fill="#33241b" />
      <ellipse cx="23.6" cy="20" rx="1.6" ry="2.2" fill="#33241b" />
    </svg>
  )
}

function SectionRow({ title, desc, onClick }: { title: string; desc: string; onClick: () => void }) {
  return (
    <button className="me-row" type="button" onClick={onClick}>
      <span className="me-row-text">
        {title}
        <small>{desc}</small>
      </span>
      <ChevronRight size={18} strokeWidth={2} aria-hidden="true" />
    </button>
  )
}

export default function ProfilePage() {
  const { user, setUser } = useAuth()
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
  const [datingGoal, setDatingGoal] = useState('')
  const [lookingFor, setLookingFor] = useState('')
  // 滑桿一定有值；ageTouched 分辨「真的設過」與「只是打開看看」，
  // 沒動過就不送，避免把未設定寫成 18/99（見 AgeRangeSlider 的註解）
  const [ageMin, setAgeMin] = useState(AGE_FLOOR)
  const [ageMax, setAgeMax] = useState(AGE_CEIL)
  const [ageTouched, setAgeTouched] = useState(false)
  const [locationArea, setLocationArea] = useState('')
  const [locating, setLocating] = useState(false)
  const [locateNote, setLocateNote] = useState('')
  const [distanceMode, setDistanceMode] = useState<DistanceMode>('km')
  const [distanceKm, setDistanceKm] = useState('')
  const [preferredArea, setPreferredArea] = useState('')
  const [languages, setLanguages] = useState<string[]>([])
  const [activeTime, setActiveTime] = useState('')
  const [voiceFirst, setVoiceFirst] = useState(false)
  const [meetSoon, setMeetSoon] = useState(false)
  const [photos, setPhotos] = useState<string[]>([])
  const [photosDirty, setPhotosDirty] = useState(false)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [showPreview, setShowPreview] = useState(false)
  // 目前在編輯哪一區；null 是總覽（先看到別人眼中的自己）。
  // 以前整頁就是一長串表單，要先按「編輯」才能改，而且看不到自己在別人眼中的樣子。
  const [section, setSection] = useState<Section>(null)
  const isEditing = section !== null
  const navigate = useNavigate()
  const [toast, setToast] = useState('')
  const toastTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const dragIndex = useRef<number | null>(null)

  // 預覽中再點底部「我的」→ 關閉預覽，回到自我介紹編輯頁
  useEffect(() => {
    function onReselect() { setShowPreview(false) }
    window.addEventListener('nav-reselect-profile', onReselect)
    return () => window.removeEventListener('nav-reselect-profile', onReselect)
  }, [])

  // 用定位帶入所在縣市。onboarding 時按了「先不用」或當時沒定到的人，回頭補的入口。
  // 只是把選單改成定到的縣市，跟其他欄位一樣要按儲存才會寫進去。
  async function handleLocate() {
    if (locating) return
    setLocating(true)
    setLocateNote('')
    const result = await locateMyArea()
    setLocating(false)
    if (result.status === 'ok') {
      setLocationArea(result.area)
      setLocateNote(`已帶入${result.area}，記得按儲存`)
    } else {
      const copy = locateFailureCopy(result, 'profile')
      setLocateNote(`${copy.title}。${copy.detail}`)
    }
  }

  function loadProfile() {
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
        setDatingGoal(res.data.datingGoal ?? '')
        setLookingFor(res.data.lookingFor ?? '')
        setAgeMin(res.data.ageMin ?? AGE_FLOOR)
        setAgeMax(res.data.ageMax ?? AGE_CEIL)
        if (res.data.ageMin != null || res.data.ageMax != null) setAgeTouched(true)
        setLocationArea(res.data.locationArea ?? '')
        setDistanceKm(res.data.distanceKm != null ? String(res.data.distanceKm) : '')
        setPreferredArea(res.data.preferredArea ?? '')
        // 在 onboarding 選了「依縣市」的人，回到這頁要看到的是縣市那一格，
        // 不然他只會看到一個空的距離輸入框，以為自己的設定不見了。
        setDistanceMode(res.data.preferredArea ? 'area' : 'km')
        setLanguages(res.data.languages ?? [])
        setActiveTime(res.data.activeTime ?? '')
        setVoiceFirst(res.data.voiceFirst ?? false)
        setMeetSoon(res.data.meetSoon ?? false)
        const loaded = res.data.photoDataUrls ?? []
        // 沒放照片就是空的。以前這裡會塞三張外部網站的隨機風景照，還標成「主照片」——
        // 使用者會以為自己放了照片，而且每次打開這頁都把請求送到外站。
        setPhotos(loaded)
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }

  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { loadProfile() }, [user?.userId])

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

  function toggleLanguage(tag: string) {
    setLanguages(prev => prev.includes(tag) ? prev.filter(t => t !== tag) : [...prev, tag])
  }

  async function handleAddPhotos(files: FileList) {
    const remaining = MAX_PHOTOS - photos.length
    if (remaining <= 0) return
    const picked = Array.from(files).slice(0, remaining)
    const dataUrls = await Promise.all(picked.map(fileToResizedBase64))
    // 上傳物件儲存，存 URL。失敗就說一聲——以前是悄悄把整串 base64 當成網址
    // 存進 users.photo_data_urls，而配對 API 每次都把整張 users 表載進記憶體。
    try {
      const urls = await Promise.all(dataUrls.map(d => uploadMedia(d, 'avatars')))
      setPhotos(prev => [...prev, ...urls])
      setPhotosDirty(true)
    } catch (err) {
      showToast(uploadErrorMessage(err))
    }
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
      showToast('請選擇性別（必填）')
      return
    }
    if (birthday && !isAdultBirthday(birthday)) {
      showToast('未滿 18 歲暫時不能使用同頻 Today')
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
        datingGoal: datingGoal || undefined,
        lookingFor: lookingFor || undefined,
        ageMin: ageTouched ? ageMin : undefined,
        ageMax: ageTouched ? ageMax : undefined,
        locationArea,
        // 距離與縣市擇一：目前方式的值照送，另一個明確清掉。
        // 距離留白送 0（清掉）而不是 null——null 在這支端點是「這次不改」，
        // 以前留白存檔其實什麼都沒清，設過的距離永遠拿不掉。
        distanceKm: distanceMode === 'km' ? clampDistance(distanceKm) : 0,
        preferredArea: distanceMode === 'area' ? preferredArea : '',
        languages,
        activeTime: activeTime || undefined,
        voiceFirst,
        meetSoon,
        ...(photosDirty ? { photoDataUrls: photos } : {}),
      })
      // 暱稱存進 profile 後，同步更新登入態，讓左上角標題（讀 user.nickname）也跟著變、重整後仍在
      const nextNickname = nickname.trim()
      if (nextNickname && nextNickname !== user.nickname) {
        setUser({ ...user, nickname: nextNickname })
      }
      setSection(null)
      showToast('已儲存')
    } catch (err) {
      showToast(err instanceof Error ? err.message : '儲存失敗')
    }
    setSaving(false)
  }

  if (!user) return null

  function openSection(next: Exclude<Section, null>) {
    setLocateNote('')
    setSection(next)
  }

  // 離開編輯不存：把畫面上改過的值丟掉，重新讀一次
  function cancelEdit() {
    setLocateNote('')
    setSection(null)
    setPhotosDirty(false)
    loadProfile()
  }

  const meta = [
    birthday ? `${getAge(birthday)}` : '',
    birthday ? getZodiac(birthday) : '',
    locationArea,
  ].filter(Boolean).join(' · ')

  return (
    <div className={`page profile-page${isEditing ? ' is-editing' : ''}`}>
      {section === null ? (
        <header className="me-header">
          <h1>我的</h1>
          {/* 通知和設定收在這裡。以前設定佔底部導覽一格，通知中心則完全沒有入口。 */}
          <button className="me-icon-btn" type="button" onClick={() => navigate('/notifications')} aria-label="通知">
            <Bell size={21} strokeWidth={2} />
          </button>
          <button className="me-icon-btn" type="button" onClick={() => navigate('/settings')} aria-label="設定">
            <Settings size={21} strokeWidth={2} />
          </button>
        </header>
      ) : (
        <header className="me-header is-sub">
          <button className="me-back" type="button" onClick={cancelEdit} aria-label="返回，不儲存">‹</button>
          <h1>{SECTION_TITLE[section]}</h1>
        </header>
      )}

      {section === null && (
        <>
          <p className="me-label">別人看到的你</p>
          <section className="me-card">
            <button className="me-card-main" type="button" onClick={() => setShowPreview(true)}>
              <span className="me-avatar">
                {photos[0] ? <img src={photos[0]} alt="" /> : <MeAvatar />}
              </span>
              <span className="me-card-id">
                <b>{nickname || user.nickname}</b>
                {meta && <small>{meta}</small>}
              </span>
              <span className="me-card-peek">預覽</span>
            </button>
            {loading ? (
              <div className="profile-loading-bar" />
            ) : (
              <p className={`me-bio${bio ? '' : ' is-empty'}`}>{bio || '還沒寫自我介紹'}</p>
            )}
            {interestTags.length > 0 && (
              <div className="me-tags">
                {interestTags.slice(0, 6).map(tag => <span key={tag}>{tag}</span>)}
                {interestTags.length > 6 && <span>+{interestTags.length - 6}</span>}
              </div>
            )}
          </section>

          {!loading && photos.length === 0 && (
            <div className="me-photo-prompt">
              <div>
                還沒放照片
                <small>放一張，別人比較容易記得你</small>
              </div>
              <button
                className="keycap-btn"
                type="button"
                onClick={() => { openSection('photos'); fileInputRef.current?.click() }}
              >
                加照片
              </button>
            </div>
          )}

          <section className="me-card me-rows">
            {photos.length > 0 && (
              <SectionRow title="照片" desc={`${photos.length} 張，第一張是主照片`} onClick={() => openSection('photos')} />
            )}
            <SectionRow title="基本資料" desc="暱稱、生日、性別、所在縣市" onClick={() => openSection('basic')} />
            <SectionRow title="關於我" desc="自我介紹、興趣、個性、外貌" onClick={() => openSection('about')} />
            <SectionRow title="配對偏好" desc="想認識誰、年齡、距離" onClick={() => openSection('prefs')} />
          </section>
        </>
      )}

      {section === 'photos' && (
        <section className="panel profile-photos-panel">
          <p className="setting-section-title profile-field-label" style={{ marginBottom: 0 }}>
            照片 <span className="profile-hint">· 第一張為主照片，可拖曳排序</span>
          </p>
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
        </section>
      )}

      {/* 檔案選擇器一直都在：總覽上的「加照片」也要點得到它 */}
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        multiple
        style={{ display: 'none' }}
        onChange={e => { if (e.target.files) handleAddPhotos(e.target.files); e.target.value = '' }}
      />

      {section === 'basic' && (
        <div className="profile-fields">
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

      {/* 性別（必填，鎖定不可改）*/}
      <section className="panel">
        <FieldLabel title="性別" required />
        {/* 選過就鎖住；還沒有值的人（例如用社群帳號註冊）要能選一次，不然這頁永遠存不了 */}
        <SingleChips options={GENDER_OPTIONS} value={genderLabel(gender)} onChange={setGender} allowClear={false} disabled={!!gender} />
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

      {/* 所在縣市：會顯示在配對卡上，也是距離偏好的起點。
          別人指定縣市或距離時拿這一欄比對，沒填的人對他們是隱形的。 */}
      <section className="panel">
        <FieldLabel title="所在縣市" optional />
        <select className="profile-text-input" aria-label="所在縣市" value={locationArea} onChange={e => { setLocationArea(e.target.value); setLocateNote('') }}>
          <option value="">不填</option>
          {AREA_OPTIONS.map(area => <option key={area} value={area}>{area}</option>)}
        </select>
        {isEditing && (
          <div className="profile-locate">
            <button type="button" className="profile-locate-btn" disabled={locating} onClick={handleLocate}>
              <LocateFixed size={14} strokeWidth={2.2} aria-hidden="true" />
              {locating ? '定位中⋯' : '用定位帶入'}
            </button>
            <span className="profile-hint" role="status" aria-live="polite">
              {locateNote || '只用來判斷縣市，座標不會上傳'}
            </span>
          </div>
        )}
      </section>

        </div>
      )}

      {section === 'about' && (
        <div className="profile-fields">
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
          editing={isEditing}
        />
      </section>

      {/* 個性 */}
      <section className="panel">
        <FieldLabel title="個性" optional mode="multi" />
        <CompactTagPicker
          options={PERSONALITY_OPTIONS}
          values={personalityTags}
          onToggle={togglePersonality}
          editing={isEditing}
        />
      </section>

      {/* 外貌 */}
      <section className="panel">
        <FieldLabel title="外貌" optional mode="multi" />
        <CompactTagPicker
          options={APPEARANCE_OPTIONS}
          values={appearanceTags}
          onToggle={toggleAppearance}
          editing={isEditing}
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

        </div>
      )}

      {section === 'prefs' && (
        <div className="profile-fields">
      {/* ── 交友意圖 ──
          註：後端 MatchService.MatchesPreferences 用 lookingFor、ageMin/ageMax 與地區
          （distanceKm 或 preferredArea，擇一）篩選候選，都是硬界線。
          datingGoal / languages / activeTime / voiceFirst / meetSoon 只有存檔，尚未進配對邏輯。
          這是內部資訊，UI 不對使用者標示。 */}

      <section className="panel">
        <FieldLabel title="交友目的" optional mode="single" />
        <SingleChips options={DATING_GOAL_OPTIONS} value={datingGoal} onChange={setDatingGoal} />
      </section>

      <section className="panel">
        <FieldLabel title="想認識的對象" optional mode="single" />
        <SingleChips options={LOOKING_FOR_OPTIONS} value={lookingFor} onChange={setLookingFor} />
      </section>

      <section className="panel">
        <FieldLabel title="年齡偏好" optional />
        <AgeRangeSlider
          className="profile-age-range"
          label=""
          min={ageMin}
          max={ageMax}
          onChange={(lo, hi) => { setAgeMin(lo); setAgeMax(hi); setAgeTouched(true) }}
        />
      </section>

      {/* 距離偏好：依距離或依縣市，擇一（和 onboarding 同一組選項）。
          以前這頁只有距離輸入框——在 onboarding 選了「依縣市」的人回來改不了，
          而且這兩欄當時都只有存檔，沒有進配對。 */}
      <section className="panel">
        <FieldLabel title="距離偏好" optional />
        <SingleChips
          options={DISTANCE_MODE_OPTIONS}
          value={DISTANCE_MODE_LABELS[distanceMode]}
          onChange={label => setDistanceMode(label === DISTANCE_MODE_LABELS.area ? 'area' : 'km')}
          allowClear={false}
        />
        <div className="profile-distance-value">
          {distanceMode === 'km' ? (
            <>
              <div className="profile-field-unit">
                <input className="profile-text-input" type="number" inputMode="numeric" min={DISTANCE_KM_MIN} max={DISTANCE_KM_MAX} placeholder="不限" aria-label="距離偏好" value={distanceKm} onChange={e => setDistanceKm(e.target.value)} />
                <span className="profile-unit">km</span>
              </div>
              {/* 距離是縣市對縣市的估算，不是定位。沒有起點時直接說，
                  而不是讓使用者填了數字卻安靜地不生效。 */}
              <span className="profile-hint profile-hint-block">
                {locationArea ? '以雙方所在的縣市估算，同縣市一律算在內' : '要先填上面的所在縣市，才算得出距離'}
              </span>
            </>
          ) : (
            <select className="profile-text-input" aria-label="偏好縣市" value={preferredArea} onChange={e => setPreferredArea(e.target.value)}>
              <option value="">不限縣市</option>
              {AREA_OPTIONS.map(area => <option key={area} value={area}>{area}</option>)}
            </select>
          )}
        </div>
      </section>

      <section className="panel">
        <FieldLabel title="語言" optional mode="multi" />
        <TagPicker options={LANGUAGE_OPTIONS} values={languages} onToggle={toggleLanguage} />
      </section>

      <section className="panel">
        <FieldLabel title="活動時段" optional mode="single" />
        <SingleChips options={ACTIVE_TIME_OPTIONS} value={activeTime} onChange={setActiveTime} />
      </section>

      <section className="panel">
        <FieldLabel title="互動偏好" optional />
        <div className="ob-tags">
          <button type="button" className={`ob-tag${voiceFirst ? ' selected' : ''}`} onClick={() => setVoiceFirst(v => !v)}>願意先語音聊</button>
          <button type="button" className={`ob-tag${meetSoon ? ' selected' : ''}`} onClick={() => setMeetSoon(v => !v)}>願意線下快見</button>
        </div>
      </section>
        </div>
      )}

      {/* 儲存：吸附底部（僅編輯時） */}
      {isEditing && (
        <div className="profile-save-bar">
          <button
            className="keycap-btn"
            type="button"
            disabled={saving || loading}
            onClick={save}
          >
            {saving ? '儲存中' : '儲存'}
          </button>
        </div>
      )}

      {toast && <div className="center-toast">{toast}</div>}

      {showPreview && (
        <ProfileCardFullscreen
          user={user}
          bio={bio}
          birthday={birthday}
          interestTags={interestTags}
          personalityTags={personalityTags}
          appearanceTags={appearanceTags}
          relationship={relationship}
          photos={photos}
          onClose={() => setShowPreview(false)}
          onGoAddPhoto={() => { openSection('photos'); setTimeout(() => fileInputRef.current?.click(), 300) }}
        />
      )}
    </div>
  )
}
