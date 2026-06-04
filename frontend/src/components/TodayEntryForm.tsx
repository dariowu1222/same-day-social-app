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
  soulNote: SoulNote | null
  isSoulNoteOpen: boolean
  onQuickEntrySelect: (entry: TodayQuickEntry) => void
  onSoulNoteToggle: () => void
  onContentChange: (value: string) => void
  onSubmit: () => void
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
  soulNote,
  isSoulNoteOpen,
  onQuickEntrySelect,
  onSoulNoteToggle,
  onContentChange,
  onSubmit,
}: Props) {
  return (
    <section className="today-entry-card">
      <div className="quick-entry-grid" aria-label="快速選擇今天的感覺">
        {quickEntries.map((entry) => (
          <button
            key={entry.key}
            type="button"
            className={`quick-entry-button quick-entry-${entry.key}`}
            onClick={() => onQuickEntrySelect(entry)}
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
        disabled={isSubmitting || content.trim().length === 0}
      >
        {isSubmitting ? '正在找同頻的人…' : '找找今天懂我的人'}
      </button>
    </section>
  )
}
