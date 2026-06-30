export type PageKey = 'today' | 'rant' | 'chat' | 'profile' | 'settings'

const items: { key: PageKey; label: string }[] = [
  { key: 'today', label: '今日' },
  { key: 'rant', label: '樹洞' },
  // { key: 'tasks', label: '任務' }, // 任務功能暫時下架（後端保留）
  { key: 'chat', label: '聊天' },
  { key: 'profile', label: '我的' },
  { key: 'settings', label: '設定' },
]

type Props = {
  activePage: PageKey
  onChange: (page: PageKey) => void
}

export default function BottomNav({ activePage, onChange }: Props) {
  return (
    <nav className="bottom-nav" aria-label="主要導覽">
      {items.map((item) => (
        <button
          key={item.key}
          className={`nav-${item.key}${activePage === item.key ? ' active' : ''}`}
          onClick={() => onChange(item.key)}
        >
          {item.label}
        </button>
      ))}
    </nav>
  )
}
