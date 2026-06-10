import { useTheme } from '../context/ThemeContext'

export type TodayQuickEntry = {
  key: string
  label: string
  helper: string
  prompt: string
  responseMode: string
  emoji: string
}

export type SoulNote = {
  quote: string
  author: string
  source: string
  note: string
}

type Props = {
  content: string
  placeholder: string
  isSubmitting: boolean
  hasSelectedEntry: boolean
  selectedEntryKey?: string | null
  soulNote: SoulNote | null
  isSoulNoteOpen: boolean
  onQuickEntrySelect: (entry: TodayQuickEntry) => void
  onSoulNoteToggle: () => void
  onContentChange: (value: string) => void
  onSubmit: () => void
}

function spawnCardParticles(cx: number, cy: number) {
  const count = 6 + Math.floor(Math.random() * 3)
  const colors = ['#b8f5a0', '#f5e8a0']
  for (let i = 0; i < count; i++) {
    const el = document.createElement('div')
    const size = 2 + Math.random() * 3
    const color = colors[Math.floor(Math.random() * colors.length)]
    el.style.cssText = `position:fixed;left:${cx}px;top:${cy}px;width:${size}px;height:${size}px;border-radius:50%;background:${color};box-shadow:0 0 ${size * 2}px ${size}px ${color};pointer-events:none;z-index:200;transform:translate(-50%,-50%);`
    document.body.appendChild(el)
    const angle = (Math.PI * 2 * i) / count + Math.random() * 0.5
    const dist = 35 + Math.random() * 55
    el.animate(
      [
        { transform: 'translate(-50%,-50%)', opacity: 1 },
        { transform: `translate(calc(-50% + ${(Math.cos(angle) * dist).toFixed(0)}px),calc(-50% + ${(Math.sin(angle) * dist).toFixed(0)}px))`, opacity: 0 },
      ],
      { duration: 500 + Math.random() * 400, easing: 'ease-out', fill: 'forwards' }
    ).onfinish = () => el.remove()
  }
}

const quickEntries: TodayQuickEntry[] = [
  {
    key: 'tired',
    label: '有點累',
    helper: '想先被接住',
    prompt: '今天其實有點累，想找個人聽我說說...',
    responseMode: 'COMFORT_ME',
    emoji: '🌧',
  },
  {
    key: 'wronged',
    label: '有點委屈',
    helper: '想被理解',
    prompt: '今天有件事讓我覺得有點委屈，明明...',
    responseMode: 'COMFORT_ME',
    emoji: '🥺',
  },
  {
    key: 'talk',
    label: '想找人說說',
    helper: '不用急著解決',
    prompt: '今天有個瞬間，我突然很想找人說說...',
    responseMode: 'JUST_LISTEN',
    emoji: '💬',
  },
  {
    key: 'good',
    label: '今天還不錯',
    helper: '分享一點亮光',
    prompt: '今天有一件小事讓我覺得還不錯...',
    responseMode: 'DISTRACT_ME',
    emoji: '✨',
  },
]

export default function TodayEntryForm({
  content,
  placeholder,
  isSubmitting,
  hasSelectedEntry,
  selectedEntryKey,
  soulNote,
  isSoulNoteOpen,
  onQuickEntrySelect,
  onSoulNoteToggle,
  onContentChange,
  onSubmit,
}: Props) {
  const { mode } = useTheme()
  // 文字欄為選填（未來付費配對功能），只要選卡即可送出
  const canSubmit = hasSelectedEntry

  return (
    <section className="today-entry-card">
      <div className="quick-entry-grid" aria-label="快速選擇今天的感覺">
        {quickEntries.map((entry) => (
          <button
            key={entry.key}
            type="button"
            className={`quick-entry-button quick-entry-${entry.key}${selectedEntryKey === entry.key ? ' selected' : ''}`}
            onClick={(e) => {
              if (mode === 'night') {
                const rect = e.currentTarget.getBoundingClientRect()
                spawnCardParticles(rect.left + rect.width / 2, rect.top + rect.height / 2)
              }
              onQuickEntrySelect(entry)
            }}
          >
            <span className="quick-entry-emoji">{entry.emoji}</span>
            <strong>{entry.label}</strong>
            <span>{entry.helper}</span>
          </button>
        ))}
      </div>

      {soulNote && (
        <section className={`soul-note ${isSoulNoteOpen ? 'open' : ''}`}>
          <button
            type="button"
            className="soul-note-toggle"
            aria-expanded={isSoulNoteOpen}
            onClick={onSoulNoteToggle}
          >
            <span>心靈小語</span>
            <svg
              className={`soul-note-chevron${isSoulNoteOpen ? ' open' : ''}`}
              width="18"
              height="18"
              viewBox="0 0 18 18"
              fill="none"
              aria-hidden="true"
            >
              <path d="M4.5 6.75L9 11.25L13.5 6.75" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </button>
          <div className="soul-note-body">
            <div>
              <blockquote>{soulNote.quote}</blockquote>
              <p className="soul-note-author">--{soulNote.author}</p>
              <p className="soul-note-text">{soulNote.note}</p>
            </div>
          </div>
        </section>
      )}

      <label className="today-note-field">
        <span>寫給今天的一小段話</span>
        <textarea
          rows={5}
          value={content}
          onChange={(event) => onContentChange(event.target.value)}
          placeholder={placeholder}
        />
      </label>

      <button
        className="today-submit-btn"
        onClick={onSubmit}
        disabled={isSubmitting || !canSubmit}
      >
        {isSubmitting ? '正在找同頻的人…' : '找找今天懂我的人'}
      </button>
    </section>
  )
}
