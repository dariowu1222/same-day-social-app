import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { ChevronLeft } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { getSettings, updateSettings, type UserSetting } from '../auth/api'

const DEFAULTS: UserSetting = {
  profilePublic: true, pauseMatching: false,
  notifyMatch: true, notifyMessage: true, notifyRant: true,
}

export default function NotificationSettingsPage() {
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
        <span className="detail-topbar-title">通知</span>
      </div>

      <section className="panel" style={{ marginTop: 12 }}>
        <p className="setting-section-title">回應提醒 <span className="profile-hint">· 推播上線後生效</span></p>
        <ToggleRow label="配對通知" hint="有人與你同頻時提醒" on={s.notifyMatch} disabled={loading} onToggle={() => toggle('notifyMatch')} />
        <ToggleRow label="聊天訊息" hint="收到新訊息時提醒" on={s.notifyMessage} disabled={loading} onToggle={() => toggle('notifyMessage')} />
        <ToggleRow label="樹洞回應" hint="你的樹洞被回覆時提醒" on={s.notifyRant} disabled={loading} onToggle={() => toggle('notifyRant')} />
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
