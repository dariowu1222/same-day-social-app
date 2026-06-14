import { useEffect, useMemo, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { createTodayEntry } from './api'
import TodayEntryForm, { type SoulNote, type TodayQuickEntry } from './TodayEntryForm'

type Props = {
  onGoToMatches: (label: string) => void
}

type SoulNoteGroup = SoulNote & {
  tone: string
}

const INTRO_KEY = 'today-intro-seen-session'

function playKeyClick() {
  try {
    const ctx = new (window.AudioContext || (window as any).webkitAudioContext)()
    const buf = ctx.createBuffer(1, ctx.sampleRate * 0.04, ctx.sampleRate)
    const data = buf.getChannelData(0)
    for (let i = 0; i < data.length; i++) {
      data[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / data.length, 6)
    }
    const src = ctx.createBufferSource()
    src.buffer = buf
    const gain = ctx.createGain()
    gain.gain.setValueAtTime(0.18, ctx.currentTime)
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.04)
    src.connect(gain)
    gain.connect(ctx.destination)
    src.start()
  } catch {
    // 靜默失敗
  }
}

const soulNotesByEntry: Record<string, SoulNoteGroup[]> = {
  tired: [
    {
      tone: 'mint',
      author: '心理學家 Carl Rogers｜人本心理學代表人物',
      quote: '當我如實接納自己，我才有可能開始改變。',
      source: 'On Becoming a Person',
      note: '有些疲憊不是因為你不夠努力，而是你已經努力很久了。能夠承認自己累了，本身就是一種誠實，也是一個人重新靠近自己的開始。',
    },
    {
      tone: 'sage',
      author: '精神科醫師 Viktor Frankl｜意義治療創始人',
      quote: '當我們無法再改變處境，就需要回頭改變自己。',
      source: "Man's Search for Meaning",
      note: '有些日子不一定能立刻變輕，但人仍然可以在很小的地方保留一點選擇。先讓今天停在這裡，也是一種溫柔的整理。',
    },
  ],
  wronged: [
    {
      tone: 'soft',
      author: '心理學家 Carl Rogers｜個人中心治療創始人',
      quote: '人就像日落一樣美好，只要讓他如其所是。',
      source: 'A Way of Being',
      note: '委屈常常來自一種沒有被看見的真實。當一個人的感受暫時無法被理解，並不代表那些感受不存在，也不代表你需要急著否定自己。',
    },
    {
      tone: 'peach',
      author: '心理學家 Daniel Kahneman｜2002 諾貝爾經濟學獎',
      quote: '當你正在想一件事時，它往往會顯得比實際上更重要。',
      source: 'Thinking, Fast and Slow',
      note: '被誤會的當下，心裡的重量會被放得很大。那份難受是真的，但它不一定等於你的全部；它只是今天某個片刻，正在用力地被你感覺到。',
    },
  ],
  talk: [
    {
      tone: 'sky',
      author: '心理學家 Albert Bandura｜美國國家科學獎章',
      quote: '除非人相信自己的行動能帶來影響，否則很少有動機開始行動。',
      source: 'Self-Efficacy: The Exercise of Control',
      note: '想說出口的念頭，通常代表心裡有一部分還在尋找連結。即使只是很短的一段話，也可能讓原本堵住的感覺，慢慢有一個可以呼吸的位置。',
    },
    {
      tone: 'sage',
      author: '精神科醫師 Viktor Frankl｜意義治療創始人',
      quote: '當我們無法再改變處境，就需要回頭改變自己。',
      source: "Man's Search for Meaning",
      note: '有時候，人不是需要立刻得到答案，而是需要有一個地方能容納還沒有答案的自己。能把話放下來一點，心也會比較不孤單。',
    },
  ],
}

export default function TodayPage({ onGoToMatches }: Props) {
  const { user } = useAuth()
  const FULL_TEXT = '今天的你，想被怎麼理解？'
  const [introPhase, setIntroPhase] = useState<'center' | 'rising' | 'done'>(
    sessionStorage.getItem(INTRO_KEY) ? 'done' : 'center'
  )
  const [typedCount, setTypedCount] = useState(0)
  const [showCursor, setShowCursor] = useState(true)
  const [content, setContent] = useState('')
  const [currentPrompt, setCurrentPrompt] = useState('不用想得太正式，像跟朋友說話一樣就好。')
  const [message, setMessage] = useState('')
  const isSubmitting = false
  const [selectedQuickEntryKey, setSelectedQuickEntryKey] = useState<string | null>(null)
  const [selectedResponseMode, setSelectedResponseMode] = useState<string>('JUST_LISTEN')
  const [selectedMoodLabel, setSelectedMoodLabel] = useState<string>('')
  const [isSoulNoteOpen, setIsSoulNoteOpen] = useState(false)

  const soulNote = useMemo(() => {
    if (!selectedQuickEntryKey) return null
    const notes = soulNotesByEntry[selectedQuickEntryKey]
    if (!notes || notes.length === 0) return null
    return notes[Math.floor(Math.random() * notes.length)]
  }, [selectedQuickEntryKey])

  const todayTone = soulNote?.tone ?? 'soft'

  // 打字機：逐字顯示
  useEffect(() => {
    if (introPhase !== 'center') return
    if (typedCount < FULL_TEXT.length) {
      const t = setTimeout(() => {
        playKeyClick()
        setTypedCount((n) => n + 1)
      }, 100)
      return () => clearTimeout(t)
    }
    // 全字打完 → 停頓 1.2s → 游標淡出 → 上飄
    const pause = setTimeout(() => {
      setShowCursor(false)
      setTimeout(() => setIntroPhase('rising'), 400)
    }, 1200)
    return () => clearTimeout(pause)
  }, [introPhase, typedCount])

  // rising → done
  useEffect(() => {
    if (introPhase === 'rising') {
      const t = setTimeout(() => {
        sessionStorage.setItem(INTRO_KEY, '1')
        setIntroPhase('done')
      }, 950)
      return () => clearTimeout(t)
    }
  }, [introPhase])

  function selectQuickEntry(entry: TodayQuickEntry) {
    setSelectedQuickEntryKey(entry.key)
    setSelectedResponseMode(entry.responseMode)
    setSelectedMoodLabel(entry.label)
    setIsSoulNoteOpen(false)
    setContent('')
    setCurrentPrompt(entry.prompt)
    setMessage('')
  }

  function submitToday() {
    // 先導頁，API 呼叫背景執行（不阻塞 UI）
    onGoToMatches(selectedMoodLabel)
    if (!user) return
    createTodayEntry({ userId: user.userId, content, responseMode: selectedResponseMode, visibility: 'MATCH_ONLY' })
      .catch(() => {/* 靜默失敗，假資料仍正常顯示 */})
  }

  return (
    <div className={`page today-page today-tone-${todayTone}`} style={{ display: 'flex', flexDirection: 'column', gap: '44px' }}>

      {/* 首次進場動畫 overlay */}
      {introPhase !== 'done' && (
        <div
          className={`today-intro-overlay today-intro-${introPhase}`}
          onClick={() => {
            sessionStorage.setItem(INTRO_KEY, '1')
            setIntroPhase('done')
          }}
        >
          <h1 className="today-intro-headline">
            {FULL_TEXT.slice(0, typedCount)}
            <span className={`today-intro-cursor${showCursor ? '' : ' hidden'}`} />
          </h1>
        </div>
      )}

      <header className={`today-header${introPhase === 'center' ? ' today-header-hidden' : ''}`}>
        <h1>今天的你，想被怎麼理解？</h1>
      </header>
      <TodayEntryForm
        content={content}
        isSubmitting={isSubmitting}
        hasSelectedEntry={selectedQuickEntryKey !== null}
        selectedEntryKey={selectedQuickEntryKey}
        soulNote={soulNote}
        isSoulNoteOpen={isSoulNoteOpen}
        onQuickEntrySelect={selectQuickEntry}
        onSoulNoteToggle={() => setIsSoulNoteOpen((current) => !current)}
        placeholder={currentPrompt}
        onContentChange={setContent}
        onSubmit={submitToday}
      />
      {message && <p className="notice">{message}</p>}
    </div>
  )
}
