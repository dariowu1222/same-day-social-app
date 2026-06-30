import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { Sun, Moon, ChevronRight } from 'lucide-react'
import { useTheme, type ThemePreference } from '../../shared/theme/ThemeContext'
import { getUnreadCount } from '../notifications/api'

const PREFS: { value: ThemePreference; icon?: 'sun' | 'moon'; label?: string }[] = [
  { value: 'day', icon: 'sun' },
  { value: 'night', icon: 'moon' },
  { value: 'auto', label: '自動' },
]

export default function SettingsPage() {
  const navigate = useNavigate()
  const { preference, isAnimating, setPreference } = useTheme()
  const [unreadNotif, setUnreadNotif] = useState(0)

  useEffect(() => {
    getUnreadCount().then(res => setUnreadNotif(res.data)).catch(() => {})
  }, [])

  return (
    <div className="page profile-page">
      <header className="page-header">
        <h1>設定</h1>
      </header>

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
              {icon === 'sun' && <Sun size={18} strokeWidth={1.8} />}
              {icon === 'moon' && <Moon size={18} strokeWidth={1.8} />}
              {label && <span className="theme-seg-label">{label}</span>}
            </button>
          ))}
        </div>
      </section>

      {/* 通知中心 */}
      <section className="panel">
        <button className="profile-nav-row" type="button" onClick={() => navigate('/notifications')}>
          <span>通知中心</span>
          {unreadNotif > 0 && <span className="notif-badge">{unreadNotif > 99 ? '99+' : unreadNotif}</span>}
          <ChevronRight size={18} strokeWidth={1.8} />
        </button>
      </section>

      {/* 帳號中心 */}
      <section className="panel">
        <button className="profile-nav-row" type="button" onClick={() => navigate('/account')}>
          <span>帳號中心</span>
          <ChevronRight size={18} strokeWidth={1.8} />
        </button>
      </section>

      {/* 安全中心 */}
      <section className="panel">
        <button className="profile-nav-row" type="button" onClick={() => navigate('/security')}>
          <span>安全中心</span>
          <ChevronRight size={18} strokeWidth={1.8} />
        </button>
      </section>

      {/* 隱私與通知 */}
      <section className="panel">
        <button className="profile-nav-row" type="button" onClick={() => navigate('/settings/privacy')}>
          <span>隱私與通知</span>
          <ChevronRight size={18} strokeWidth={1.8} />
        </button>
      </section>
    </div>
  )
}
