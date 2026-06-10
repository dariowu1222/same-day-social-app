import { useState } from 'react'
import { Sun, Moon } from 'lucide-react'
import type { DemoUser } from '../App'
import { useTheme, type ThemePreference } from '../context/ThemeContext'

type Props = {
  user: DemoUser
  setUser: (user: DemoUser | null) => void
}

const PREFS: { value: ThemePreference; icon?: 'sun' | 'moon'; label?: string }[] = [
  { value: 'day',   icon: 'sun' },
  { value: 'night', icon: 'moon' },
  { value: 'auto',  label: '自動' },
]

export default function ProfilePage({ user, setUser }: Props) {
  const [bio, setBio] = useState('慢慢認識就好。')
  const [interests, setInterests] = useState('咖啡,散步,電影')
  const [values, setValues] = useState('尊重,公平,低壓陪伴')
  const { preference, isAnimating, setPreference } = useTheme()

  function logout() {
    localStorage.removeItem('same-day-demo-user')
    setUser(null)
  }

  return (
    <div className="page">
      <header className="page-header">
        <p className="eyebrow">我的</p>
        <h1>{user.nickname}</h1>
        <p>不需要真名，不顯示精準位置，也不強迫設定戀愛目的。</p>
      </header>

      {/* 外觀設定 */}
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

      <section className="panel">
        <label>
          簡介
          <textarea rows={4} value={bio} onChange={(event) => setBio(event.target.value)} />
        </label>
        <label>
          興趣標籤
          <input value={interests} onChange={(event) => setInterests(event.target.value)} />
        </label>
        <label>
          價值觀標籤
          <input value={values} onChange={(event) => setValues(event.target.value)} />
        </label>
        <button className="ghost" onClick={logout}>
          登出
        </button>
      </section>
    </div>
  )
}
