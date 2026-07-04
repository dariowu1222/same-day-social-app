import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  ChevronRight, Palette, Bell, UserCircle, Shield, Ban, HelpCircle, LogOut,
  Sun, Moon, Smartphone,
} from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { useTheme, type ThemePreference } from '../../shared/theme/ThemeContext'

const APP_VERSION = '0.1.0'

const APPEARANCE: { value: ThemePreference; label: string; Icon: typeof Sun }[] = [
  { value: 'day', label: '亮色', Icon: Sun },
  { value: 'night', label: '暗色', Icon: Moon },
  { value: 'auto', label: '自動', Icon: Smartphone },
]

type RowProps = {
  Icon: typeof Bell
  seatBg: string
  iconColor: string
  title: string
  desc?: string
  onClick: () => void
  danger?: boolean
}

function SettingRow({ Icon, seatBg, iconColor, title, desc, onClick, danger }: RowProps) {
  return (
    <button className={`set-row${danger ? ' danger' : ''}`} type="button" onClick={onClick}>
      <span className="set-row-seat" style={{ background: seatBg }}>
        <Icon size={17} strokeWidth={1.9} style={{ color: iconColor }} />
      </span>
      <span className="set-row-text">
        <span className="set-row-title">{title}</span>
        {desc && <span className="set-row-desc">{desc}</span>}
      </span>
      {!danger && <ChevronRight size={15} className="set-row-chevron" />}
    </button>
  )
}

export default function SettingsPage() {
  const navigate = useNavigate()
  const { logout } = useAuth()
  const { preference, isAnimating, setPreference } = useTheme()
  const [confirmLogout, setConfirmLogout] = useState(false)

  return (
    <div className="page settings-page">
      <header className="page-header">
        <h1>設定</h1>
      </header>

      {/* 個人化 */}
      <p className="set-group-title">個人化</p>
      <section className="set-card">
        <div className="set-appearance">
          <span className="set-appearance-seat" style={{ background: '#fdf0e4' }}>
            <Palette size={17} strokeWidth={1.9} style={{ color: '#c88a3c' }} />
          </span>
          <div className="set-appearance-body">
            <span className="set-row-title">外觀</span>
            <div className="appearance-switch" role="radiogroup" aria-label="外觀主題" aria-disabled={isAnimating}>
              {APPEARANCE.map(({ value, label, Icon }) => (
                <button
                  key={value}
                  role="radio"
                  aria-checked={preference === value}
                  className={`appearance-opt${preference === value ? ' selected' : ''}`}
                  disabled={isAnimating}
                  onClick={() => setPreference(value)}
                >
                  <Icon size={19} strokeWidth={1.9} />
                  <span>{label}</span>
                </button>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* 帳號與安全 */}
      <p className="set-group-title">帳號與安全</p>
      <section className="set-card">
        <SettingRow Icon={Bell} seatBg="#e9f2ec" iconColor="#4a8a62" title="通知" desc="推播、聊天與樹洞回應提醒" onClick={() => navigate('/settings/notifications')} />
        <SettingRow Icon={UserCircle} seatBg="#eef0f6" iconColor="#5a70a8" title="帳號" desc="Email、密碼、綁定的登入方式" onClick={() => navigate('/account')} />
        <SettingRow Icon={Shield} seatBg="#f6ecf0" iconColor="#a85a80" title="隱私" desc="誰能看到我、資料使用" onClick={() => navigate('/settings/privacy')} />
        <SettingRow Icon={Ban} seatBg="#f0ebe3" iconColor="#8a7a6a" title="封鎖名單" desc="已封鎖的人" onClick={() => navigate('/security')} />
      </section>

      {/* 其他 */}
      <p className="set-group-title">其他</p>
      <section className="set-card">
        <SettingRow Icon={HelpCircle} seatBg="#f0ebe3" iconColor="#8a7a6a" title="意見回饋" onClick={() => navigate('/settings/feedback')} />
        <SettingRow Icon={LogOut} seatBg="#fceeea" iconColor="#c0392b" title="登出" danger onClick={() => setConfirmLogout(true)} />
      </section>

      <p className="set-version">同頻 Today v{APP_VERSION}</p>

      {confirmLogout && (
        <div className="set-confirm-mask" onClick={() => setConfirmLogout(false)}>
          <div className="set-confirm" role="alertdialog" onClick={(e) => e.stopPropagation()}>
            <div className="set-confirm-title">登出</div>
            <div className="set-confirm-desc">確定要登出嗎？</div>
            <div className="set-confirm-actions">
              <button className="set-confirm-cancel" type="button" onClick={() => setConfirmLogout(false)}>取消</button>
              <button className="set-confirm-ok" type="button" onClick={logout}>登出</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
