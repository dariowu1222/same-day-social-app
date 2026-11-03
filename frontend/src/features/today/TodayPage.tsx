import { useEffect, useMemo, useRef, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { markTodayIntroSeen, shouldPlayTodayIntro } from '../../shared/lib/storageKeys'
import { createTodayEntry } from './api'
import TodayEntryForm, { type SoulNote, type TodayQuickEntry } from './TodayEntryForm'

type Props = {
  /** pendingEntry 是「存今日」那一支還在飛的請求，交給滑卡頁去等。 */
  onGoToMatches: (label: string, pendingEntry: Promise<unknown> | null) => void
}

type SoulNoteGroup = SoulNote & {
  tone: string
}

const soulNotesByEntry: Record<string, SoulNoteGroup[]> = {
  tired: [
    {
      tone: 'mint',
      author: '心理學家 Carl Rogers｜人本心理學代表人物',
      quote: '當我如實接納自己，我才有可能開始改變。',
      original: 'The curious paradox is that when I accept myself just as I am, then I can change.',
      source: 'On Becoming a Person',
      note: '有些疲憊不是因為你不夠努力，而是你已經努力很久了。能夠承認自己累了，本身就是一種誠實，也是一個人重新靠近自己的開始。',
    },
    {
      tone: 'sage',
      author: '精神科醫師 Viktor Frankl｜意義治療創始人',
      quote: '當我們無法再改變處境，就需要回頭改變自己。',
      original: 'When we are no longer able to change a situation, we are challenged to change ourselves.',
      source: "Man's Search for Meaning",
      note: '有些日子不一定能立刻變輕，但人仍然可以在很小的地方保留一點選擇。先讓今天停在這裡，也是一種溫柔的整理。',
    },
  ],
  wronged: [
    {
      tone: 'soft',
      author: '心理學家 Carl Rogers｜個人中心治療創始人',
      quote: '人就像日落一樣美好，只要讓他如其所是。',
      original: 'People are just as wonderful as sunsets if you let them be.',
      source: 'A Way of Being',
      note: '委屈常常來自一種沒有被看見的真實。當一個人的感受暫時無法被理解，並不代表那些感受不存在，也不代表你需要急著否定自己。',
    },
    {
      tone: 'peach',
      author: '心理學家 Daniel Kahneman｜2002 諾貝爾經濟學獎',
      quote: '當你正在想一件事時，它往往會顯得比實際上更重要。',
      original: 'Nothing in life is as important as you think it is while you are thinking about it.',
      source: 'Thinking, Fast and Slow',
      note: '被誤會的當下，心裡的重量會被放得很大。那份難受是真的，但它不一定等於你的全部；它只是今天某個片刻，正在用力地被你感覺到。',
    },
  ],
  talk: [
    {
      tone: 'sky',
      author: '心理學家 Albert Bandura｜美國國家科學獎章',
      quote: '除非人相信自己的行動能帶來影響，否則很少有動機開始行動。',
      original: 'Unless people believe they can produce desired effects by their actions, they have little incentive to act.',
      source: 'Self-Efficacy: The Exercise of Control',
      note: '想說出口的念頭，通常代表心裡有一部分還在尋找連結。即使只是很短的一段話，也可能讓原本堵住的感覺，慢慢有一個可以呼吸的位置。',
    },
    {
      tone: 'sage',
      author: '精神科醫師 Viktor Frankl｜意義治療創始人',
      quote: '當我們無法再改變處境，就需要回頭改變自己。',
      original: 'When we are no longer able to change a situation, we are challenged to change ourselves.',
      source: "Man's Search for Meaning",
      note: '有時候，人不是需要立刻得到答案，而是需要有一個地方能容納還沒有答案的自己。能把話放下來一點，心也會比較不孤單。',
    },
  ],
}

export default function TodayPage({ onGoToMatches }: Props) {
  const { user } = useAuth()
  const FULL_TEXT = '今天的你，想被怎麼理解？'
  const [introPhase, setIntroPhase] = useState<'center' | 'rising' | 'done'>(
    () => (shouldPlayTodayIntro() ? 'center' : 'done')
  )
  const [typedCount, setTypedCount] = useState(0)
  const [showCursor, setShowCursor] = useState(true)
  const [content, setContent] = useState('')
  const [currentPrompt, setCurrentPrompt] = useState('不用想得太正式，像跟朋友說話一樣就好。')
  const [message, setMessage] = useState('')
  // 送出只允許一次。按鈕按下去就導頁，但手機上兩次點擊可能落在同一個
  // commit 之前，不擋的話會建立兩筆今日。
  const submittedRef = useRef(false)
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
        markTodayIntroSeen()
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
    // 不清空已經打好的字。換心情通常是「我想了一下，這個詞更準」，
    // 不是「我要重寫」——把內文倒掉等於懲罰改變心意的人。
    setCurrentPrompt(entry.prompt)
    setMessage('')
  }

  function submitToday() {
    if (submittedRef.current) return
    submittedRef.current = true

    if (!user) {
      onGoToMatches(selectedMoodLabel, null)
      return
    }

    // 仍然立刻導頁（滑卡頁本來就有載入畫面，等待被它吸收掉），
    // 但把這支請求交出去讓滑卡頁先 await 再取卡。
    //
    // 先前是導完頁就不管了，造成兩個問題：
    // 一是 POST 跟滑卡頁的 GET 同時在飛，後端配對讀的是「最新一筆今日」，
    //    POST 還沒落地就會拿上一則、甚至沒有的今日去配——使用者寫了「今天很累」
    //    卻配到完全無關的人。
    // 二是 .catch 整個吞掉，存失敗時使用者寫的東西就沒了，而且沒人告訴他。
    const pending = createTodayEntry({
      userId: user.userId,
      content,
      responseMode: selectedResponseMode,
      visibility: 'MATCH_ONLY',
    })
    onGoToMatches(selectedMoodLabel, pending)
  }

  return (
    <div className={`page today-page today-tone-${todayTone}`} style={{ display: 'flex', flexDirection: 'column', gap: '44px' }}>

      {/* 首次進場動畫 overlay */}
      {introPhase !== 'done' && (
        <div
          className={`today-intro-overlay today-intro-${introPhase}`}
          onClick={() => {
            markTodayIntroSeen()
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
