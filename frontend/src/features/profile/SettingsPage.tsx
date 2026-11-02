import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  ChevronRight, Palette, Bell, UserCircle, Shield, Ban, HelpCircle, LogOut,
  Sun, Moon, Smartphone, ShieldCheck,
} from 'lucide-react'
import { checkIsAdmin } from '../../shared/admin/api'
import { useAuth } from '../auth/AuthContext'
import { useTheme, type ThemePreference } from '../../shared/theme/ThemeContext'
import { APP_VERSION_LABEL } from '../../shared/lib/appVersion'


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
  // 管理者才看得到後台入口。預設 false：問不到答案時一律當成不是管理者，
  // 寧可少顯示一列，也不要讓一般使用者看到這個東西存在。
  const [isAdmin, setIsAdmin] = useState(false)

  useEffect(() => {
    let alive = true
    void checkIsAdmin().then((value) => { if (alive) setIsAdmin(value) })
    return () => { alive = false }
  }, [])

  function openAdminConsole() {
    // 後台由後端託管在 /admin 底下；本機開發時後端沒有那份靜態檔案，
    // 走 admin 專案自己的 dev server。
    const base = import.meta.env.VITE_API_BASE_URL
    const url = base ? `${base}/admin` : 'http://localhost:5174'
    window.open(url, '_blank', 'noopener,noreferrer')
  }

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

      {/* 管理：只有白名單上的帳號看得到。入口藏起來只是避免一般使用者接觸到
          這個概念，真正的防線在後端——而且進去還要再過一次信箱驗證碼。 */}
      {isAdmin && (
        <>
          <p className="set-group-title">管理</p>
          <section className="set-card">
            <SettingRow
              Icon={ShieldCheck}
              seatBg="#e9f2ec"
              iconColor="#4a8a62"
              title="審核後台"
              desc="檢舉處理與會員管理．需信箱驗證碼"
              onClick={openAdminConsole}
            />
          </section>
        </>
      )}

      {/* 其他 */}
      <p className="set-group-title">其他</p>
      <section className="set-card">
        <SettingRow Icon={HelpCircle} seatBg="#f0ebe3" iconColor="#8a7a6a" title="意見回饋" onClick={() => navigate('/settings/feedback')} />
        <SettingRow Icon={LogOut} seatBg="#fceeea" iconColor="#c0392b" title="登出" danger onClick={() => setConfirmLogout(true)} />
      </section>

      <p className="set-version">同頻 Today {APP_VERSION_LABEL}</p>

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
