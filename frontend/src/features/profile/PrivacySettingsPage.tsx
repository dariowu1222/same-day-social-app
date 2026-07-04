import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { ChevronLeft } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { getSettings, updateSettings, type UserSetting } from '../auth/api'

const DEFAULTS: UserSetting = {
  profilePublic: true, pauseMatching: false,
  notifyMatch: true, notifyMessage: true, notifyRant: true,
}

export default function PrivacySettingsPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [s, setS] = useState<UserSetting>(DEFAULTS)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    try {
      const res = await getSettings()
      setS(res.data)
    } catch { /* ignore */ } finally {
      setLoading(false)
    }
  }, [])
  useEffect(() => { void load() }, [load])

  async function toggle(key: keyof UserSetting) {
    const prev = s
    const next = { ...s, [key]: !s[key] }
    setS(next)
    try {
      await updateSettings({ [key]: next[key] })
    } catch {
      setS(prev)
    }
  }

  if (!user) return null

  return (
    <div className="page account-page">
      <div className="detail-topbar">
        <button className="detail-back-btn" type="button" onClick={() => navigate('/settings')} aria-label="返回">
          <ChevronLeft size={22} />
        </button>
        <span className="detail-topbar-title">隱私</span>
      </div>

      <section className="panel" style={{ marginTop: 12 }}>
        <p className="setting-section-title">誰能看到我</p>
        <ToggleRow label="公開個人檔案" hint="關閉後僅配對成功的人能看到" on={s.profilePublic} disabled={loading} onToggle={() => toggle('profilePublic')} />
        <ToggleRow label="暫停配對" hint="開啟後不會出現在別人的配對中" on={s.pauseMatching} disabled={loading} onToggle={() => toggle('pauseMatching')} />
      </section>

      <section className="panel">
        <p className="setting-section-title">資料使用</p>
        <p className="account-danger-note">
          你的資料僅用於配對與服務運作，不會販售給第三方。刪除帳號可在「帳號」頁操作。
        </p>
      </section>
    </div>
  )
}

function ToggleRow({ label, hint, on, disabled, onToggle }: {
  label: string; hint?: string; on: boolean; disabled?: boolean; onToggle: () => void
}) {
  return (
    <div className="settings-row">
      <div className="settings-row-text">
        <span className="settings-row-label">{label}</span>
        {hint && <span className="settings-row-hint">{hint}</span>}
      </div>
      <button
        type="button"
        className={`settings-switch${on ? ' on' : ''}`}
        disabled={disabled}
        onClick={onToggle}
        aria-pressed={on}
        aria-label={label}
      >
        <span className="settings-switch-knob" />
      </button>
    </div>
  )
}
