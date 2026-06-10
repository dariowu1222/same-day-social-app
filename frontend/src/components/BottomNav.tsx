export type PageKey = 'today' | 'rant' | 'tasks' | 'chat' | 'profile'

const items: { key: PageKey; label: string }[] = [
  { key: 'today', label: '今日' },
  { key: 'rant', label: '樹洞' },
  { key: 'tasks', label: '任務' },
  { key: 'chat', label: '聊天' },
  { key: 'profile', label: '我的' },
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
