import { useState, useEffect, useRef } from 'react'
import { EyeOff, Camera } from 'lucide-react'
import type { DemoUser } from '../auth/types'
import { getAge, getZodiac, ZODIAC_ICON } from './profileUtils'

// 「預覽我的卡」全螢幕視圖：模擬別人在配對頁看到你的樣子。
export function ProfileCardFullscreen({
  user, bio, birthday, interestTags, photos, onClose, onGoAddPhoto,
}: {
  user: DemoUser; bio: string; birthday: string
  interestTags: string[]; photos: string[]
  onClose: () => void; onGoAddPhoto: () => void
}) {
  const [photoIndex, setPhotoIndex] = useState(0)
  const [visible, setVisible] = useState(false)
  const [bioExpanded, setBioExpanded] = useState(false)
  const [bioOverflow, setBioOverflow] = useState(false)
  const bioRef = useRef<HTMLParagraphElement>(null)

  useEffect(() => {
    requestAnimationFrame(() => setVisible(true))
    function onPopState() { handleClose() }
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  // 量測自介在收合（3 行截斷）狀態下是否有溢出，決定是否顯示「更多」
  useEffect(() => {
    const el = bioRef.current
    setBioOverflow(!!el && el.scrollHeight > el.clientHeight + 1)
  }, [bio])

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
            <div className="swipe-field">
              <p className="swipe-field-label">關於我</p>
              {bio ? (
                <>
                  <p
                    ref={bioRef}
                    className={`swipe-bio${bioExpanded ? '' : ' clamped'}`}
                  >{bio}</p>
                  {bioOverflow && (
                    <button
                      type="button"
                      className="swipe-bio-more"
                      onClick={() => setBioExpanded(v => !v)}
                    >{bioExpanded ? '收合' : '更多'}</button>
                  )}
                </>
              ) : (
                <p className="swipe-bio profile-fs-muted">還沒填自我介紹</p>
              )}
            </div>
            <div className="swipe-divider" />
            <div className="swipe-resonance-strip">
              <span className="swipe-resonance-dot" />
              <span>你今天的共鳴語句，會顯示在這裡</span>
            </div>
            {interestTags.length > 0 && (
              <div className="swipe-field">
                <p className="swipe-field-label">興趣</p>
                <div className="swipe-tags">
                  {interestTags.map(tag => <span key={tag} className="swipe-tag">{tag}</span>)}
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
