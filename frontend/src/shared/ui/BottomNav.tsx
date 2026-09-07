import { LogOut, MessageSquare, Settings, Sun, Trees, User } from 'lucide-react'
import { useAuth } from '../../features/auth/AuthContext'
import { useIsDesktop } from '../hooks/useIsDesktop'

export type PageKey = 'today' | 'rant' | 'chat' | 'profile' | 'settings'

// 五分頁：今日/樹洞/聊天/我的/設定。任務未來回歸時插在「樹洞」與「聊天」之間。
const items: { key: PageKey; label: string; Icon: typeof Sun }[] = [
  { key: 'today', label: '今日', Icon: Sun },
  { key: 'rant', label: '樹洞', Icon: Trees },
  // { key: 'tasks', label: '任務', Icon: Flag }, // 任務功能暫時下架（後端保留）
  { key: 'chat', label: '聊天', Icon: MessageSquare },
  { key: 'profile', label: '我的', Icon: User },
  { key: 'settings', label: '設定', Icon: Settings },
]

type Props = {
  activePage: PageKey
  onChange: (page: PageKey) => void
  chatUnread?: number
}

export default function BottomNav({ activePage, onChange, chatUnread = 0 }: Props) {
  // 桌面版這條導覽會變成左側直欄，有空間放品牌與使用者資訊；
  // 手機版維持底部橫條，以下兩區塊連 DOM 都不會產生。
  const isDesktop = useIsDesktop()
  const { user, logout } = useAuth()

  return (
    <nav className="bottom-nav" aria-label="主要導覽">
      {isDesktop && (
        <div className="nav-brand">
          <span className="nav-brand-mark" aria-hidden="true">同</span>
          <span className="nav-brand-text">同頻 Today</span>
        </div>
      )}

      {items.map(({ key, label, Icon }) => {
        const active = activePage === key
        return (
          <button
            key={key}
            type="button"
            className={`bn-tab nav-${key}${active ? ' active' : ''}`}
            onClick={() => onChange(key)}
            aria-current={active ? 'page' : undefined}
          >
            <span className="bn-capsule">
              <span className="bn-icon">
                <Icon size={21} strokeWidth={active ? 2.2 : 1.9} />
                {key === 'chat' && chatUnread > 0 && (
                  <span className="bn-badge">{chatUnread > 99 ? '99+' : chatUnread}</span>
                )}
              </span>
              <span className="bn-label">{label}</span>
            </span>
            </button>
        )
      })}

      {isDesktop && user && (
        <div className="nav-user">
          <span className="nav-user-avatar" aria-hidden="true">
            {user.nickname.trim()[0]?.toUpperCase() ?? '?'}
          </span>
          <span className="nav-user-name">{user.nickname}</span>
          <button type="button" className="nav-user-logout" onClick={logout} aria-label="登出">
            <LogOut size={16} strokeWidth={1.9} />
          </button>
        </div>
      )}
    </nav>
  )
}
