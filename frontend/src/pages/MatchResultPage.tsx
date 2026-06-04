import { useEffect, useRef, useState } from 'react'
import type { MatchResult } from '../api/client'

type EnrichedMatch = MatchResult & {
  age: number
  city: string
  bio: string
  zodiac: string
  interests: string[]
  avatarGradient: [string, string]
  emoji: string
}

const TUTORIAL_KEY = 'match-swipe-tutorial-done'

const FAKE_MATCHES: EnrichedMatch[] = [
  {
    matchId: '1', matchedUserId: 'u1', nickname: '晨光裡的貓',
    matchScore: 94, matchType: 'EMOTION',
    reason: '你們今天都有一種說不清的疲憊感，需要一個懂的人接住',
    todaySummary: '加班到很晚，回家路上突然不想說話，只想靜靜坐著發呆。',
    sharedTags: ['疲憊', '需要空間', '安靜'],
    icebreaker: '今天讓自己喘口氣了嗎？',
    age: 26, city: '台北', zodiac: '天蠍座',
    bio: '平常喜歡在咖啡廳發呆，偶爾寫寫日記。',
    interests: ['咖啡', '寫作', '獨處', '爵士樂'],
    avatarGradient: ['#a8d8cf', '#7bb8b0'], emoji: '🐈',
  },
  {
    matchId: '2', matchedUserId: 'u2', nickname: '雨後的窗',
    matchScore: 89, matchType: 'EMOTION',
    reason: '同樣在尋找一個可以說說話的人，不需要解決，只是想被聽見',
    todaySummary: '開了好幾個會，心裡卻覺得越來越空，說了好多話但沒說到想說的。',
    sharedTags: ['需要被聽見', '有點空', '工作壓力'],
    icebreaker: '你今天最想聊什麼？',
    age: 29, city: '台中', zodiac: '雙魚座',
    bio: '設計師，習慣用觀察取代開口。',
    interests: ['設計', '電影', '散步', '手沖咖啡'],
    avatarGradient: ['#b8d4e8', '#8ab0cc'], emoji: '🌧️',
  },
  {
    matchId: '3', matchedUserId: 'u3', nickname: '深夜書寫者',
    matchScore: 86, matchType: 'VALUE',
    reason: '你們對「累了也要撐著」這件事有一樣的感受',
    todaySummary: '今天明明撐過去了，但還是覺得有點委屈，說不出來是哪裡。',
    sharedTags: ['委屈', '撐著', '需要被理解'],
    icebreaker: '今天有沒有一個讓你覺得「還好有撐過去」的瞬間？',
    age: 31, city: '台北', zodiac: '摩羯座',
    bio: '愛讀書、寫字，睡前總是思緒紛飛。',
    interests: ['閱讀', '寫日記', '文學', '黑膠唱片'],
    avatarGradient: ['#c8b8e8', '#a090cc'], emoji: '✍️',
  },
  {
    matchId: '4', matchedUserId: 'u4', nickname: '輕聲說話的人',
    matchScore: 83, matchType: 'EMOTION',
    reason: '都在用一種很輕的方式承受不輕的事',
    todaySummary: '跟朋友說沒事，但其實沒那麼沒事，只是不知道從哪裡說起。',
    sharedTags: ['說不出口', '疲憊', '低落'],
    icebreaker: '如果今天可以跟人說一句話，你會說什麼？',
    age: 24, city: '高雄', zodiac: '天秤座',
    bio: '喜歡安靜的地方和長時間的散步。',
    interests: ['散步', '貓咖', '攝影', '植物'],
    avatarGradient: ['#f0c8b8', '#d8a090'], emoji: '🕊️',
  },
  {
    matchId: '5', matchedUserId: 'u5', nickname: '走很慢的旅人',
    matchScore: 81, matchType: 'LIFESTYLE',
    reason: '你們都習慣把感受放在心裡走一段，再慢慢說出來',
    todaySummary: '下班在路上走了很久，不想太快回家，邊走邊想今天的事。',
    sharedTags: ['需要散步', '自我空間', '慢慢來'],
    icebreaker: '今天路上有沒有看到什麼讓你停下來的東西？',
    age: 28, city: '新竹', zodiac: '射手座',
    bio: '喜歡去沒去過的街道，一個人旅行。',
    interests: ['旅行', '街拍', '在地小吃', '騎單車'],
    avatarGradient: ['#d4e8c8', '#a8cc98'], emoji: '🧳',
  },
  {
    matchId: '6', matchedUserId: 'u6', nickname: '一杯熱茶的距離',
    matchScore: 78, matchType: 'EMOTION',
    reason: '都在找一個可以陪著喝杯東西、什麼都不說也可以的人',
    todaySummary: '今天就是想有人在旁邊，不用說話也好，存在就夠了。',
    sharedTags: ['陪伴', '安靜', '需要存在感'],
    icebreaker: '如果現在可以跟誰坐著，你希望是誰？',
    age: 27, city: '台南', zodiac: '金牛座',
    bio: '茶控，相信沉默也是一種溝通。',
    interests: ['手搖茶', '烘焙', '老屋', '冥想'],
    avatarGradient: ['#e8d8b0', '#ccb888'], emoji: '🍵',
  },
  {
    matchId: '7', matchedUserId: 'u7', nickname: '低頭看腳步的人',
    matchScore: 76, matchType: 'VALUE',
    reason: '對努力這件事有一樣的溫柔和懷疑',
    todaySummary: '今天做了很多，但不確定有沒有意義，有點迷失方向。',
    sharedTags: ['努力中', '懷疑意義', '需要方向'],
    icebreaker: '你今天做的事，有哪件讓你覺得「這值得」？',
    age: 30, city: '台北', zodiac: '處女座',
    bio: '工程師，喜歡跑步和想很多事情。',
    interests: ['跑步', '健身', 'Podcast', '科幻小說'],
    avatarGradient: ['#c8d8e8', '#98b0c8'], emoji: '👣',
  },
  {
    matchId: '8', matchedUserId: 'u8', nickname: '半夜還在想事情',
    matchScore: 74, matchType: 'EMOTION',
    reason: '都有一種白天沒說出口的話，晚上突然很想說',
    todaySummary: '睡前腦子停不下來，一直想今天某個對話哪裡不對。',
    sharedTags: ['睡不著', '腦子轉', '想太多'],
    icebreaker: '你今天腦子裡跑最多的是什麼？',
    age: 25, city: '桃園', zodiac: '雙子座',
    bio: '晚睡星人，習慣凌晨兩點回想白天的事。',
    interests: ['動漫', '鋼琴', '甜點', '夜間散步'],
    avatarGradient: ['#d8c8e8', '#b0a0cc'], emoji: '🌙',
  },
  {
    matchId: '9', matchedUserId: 'u9', nickname: '偶爾需要被接住',
    matchScore: 71, matchType: 'EMOTION',
    reason: '你們今天都有一種「有點撐不住但還好」的感覺',
    todaySummary: '今天有個瞬間差點掉眼淚，但忍住了，自己都不知道為什麼。',
    sharedTags: ['忍住了', '快撐不住', '需要接住'],
    icebreaker: '那個差點的瞬間，是什麼讓你撐過去的？',
    age: 23, city: '基隆', zodiac: '巨蟹座',
    bio: '學生，喜歡海邊和雨天。',
    interests: ['海邊', '攝影', '獨立音樂', '下廚'],
    avatarGradient: ['#e8c8c8', '#cc9898'], emoji: '🌊',
  },
  {
    matchId: '10', matchedUserId: 'u10', nickname: '喜歡看窗外的人',
    matchScore: 68, matchType: 'LIFESTYLE',
    reason: '都習慣用觀察代替開口，感受很細',
    todaySummary: '今天在咖啡廳坐了很久，看著外面的人走來走去，想了好多事。',
    sharedTags: ['觀察者', '細膩', '安靜的溫柔'],
    icebreaker: '今天你觀察到什麼有趣或有點感傷的事？',
    age: 32, city: '台北', zodiac: '水瓶座',
    bio: '攝影師，習慣記錄比說話多。',
    interests: ['攝影', '展覽', '黑白電影', '咖啡廳選書'],
    avatarGradient: ['#c8e8d8', '#90c8a8'], emoji: '🪟',
  },
]

type Props = {
  onBack: () => void
}

type SwipeDir = 'left' | 'right' | null

const ZODIAC_ICON: Record<string, string> = {
  '牡羊座': '♈', '金牛座': '♉', '雙子座': '♊', '巨蟹座': '♋',
  '獅子座': '♌', '處女座': '♍', '天秤座': '♎', '天蠍座': '♏',
  '射手座': '♐', '摩羯座': '♑', '水瓶座': '♒', '雙魚座': '♓',
}

function getAvatarChar(name: string) {
  return name.charAt(0)
}

function SwipeCard({
  match,
  isTop,
  stackOffset,
  onSwipe,
}: {
  match: EnrichedMatch
  isTop: boolean
  stackOffset: number
  onSwipe: (dir: SwipeDir) => void
}) {
  const cardRef = useRef<HTMLDivElement>(null)
  const startX = useRef(0)
  const startY = useRef(0)
  const currentX = useRef(0)
  const [dragX, setDragX] = useState(0)
  const [dragging, setDragging] = useState(false)
  const [flyDir, setFlyDir] = useState<SwipeDir>(null)

  const THRESHOLD = 80

  function onPointerDown(e: React.PointerEvent) {
    if (!isTop) return
    startX.current = e.clientX
    startY.current = e.clientY
    currentX.current = 0
    setDragging(true)
    cardRef.current?.setPointerCapture(e.pointerId)
  }

  function onPointerMove(e: React.PointerEvent) {
    if (!dragging) return
    const dx = e.clientX - startX.current
    currentX.current = dx
    setDragX(dx)
  }

  function onPointerUp() {
    if (!dragging) return
    setDragging(false)
    const dx = currentX.current
    if (Math.abs(dx) >= THRESHOLD) {
      const dir: SwipeDir = dx > 0 ? 'right' : 'left'
      setFlyDir(dir)
      setTimeout(() => onSwipe(dir), 320)
    } else {
      setDragX(0)
    }
  }

  const rotate = dragX * 0.08
  // 右滑 (dragX > 0) = 同頻 (like)；左滑 (dragX < 0) = 跳過 (nope)
  const likeOpacity = Math.min(1, Math.max(0, dragX / 80))
  const nopeOpacity = Math.min(1, Math.max(0, -dragX / 80))

  const flyStyle = flyDir
    ? { transform: `translateX(${flyDir === 'right' ? 140 : -140}%) rotate(${flyDir === 'right' ? 24 : -24}deg)`, transition: 'transform 0.32s ease-in', opacity: 0 }
    : dragging
      ? { transform: `translateX(${dragX}px) rotate(${rotate}deg)`, transition: 'none' }
      : { transform: `translateX(${dragX}px) rotate(${rotate}deg) scale(${1 - stackOffset * 0.05}) translateY(${stackOffset * 14}px)`, transition: 'transform 0.3s ease' }

  const baseStyle: React.CSSProperties = {
    position: 'absolute',
    inset: 0,
    zIndex: 10 - stackOffset,
    cursor: isTop ? 'grab' : 'default',
    userSelect: 'none',
    touchAction: 'none',
    ...flyStyle,
  }

  if (!isTop && stackOffset > 0) {
    baseStyle.transform = `scale(${1 - stackOffset * 0.05}) translateY(${stackOffset * 14}px)`
    baseStyle.transition = 'transform 0.3s ease'
    baseStyle.filter = `brightness(${1 - stackOffset * 0.06})`
  }

  return (
    <div
      ref={cardRef}
      className="swipe-card"
      style={baseStyle}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={onPointerUp}
    >
      {/* 同頻 / 跳過 overlay */}
      <div className="swipe-label swipe-label-like" style={{ opacity: likeOpacity }}>同頻 ✦</div>
      <div className="swipe-label swipe-label-nope" style={{ opacity: nopeOpacity }}>跳過 ✕</div>

      {/* 頭像區 */}
      <div
        className="swipe-avatar"
        style={{ background: `linear-gradient(135deg, ${match.avatarGradient[0]}, ${match.avatarGradient[1]})` }}
      >
        <span className="swipe-avatar-emoji">{match.emoji}</span>
        <span className="swipe-avatar-char">{getAvatarChar(match.nickname)}</span>
      </div>

      {/* 卡片資訊 */}
      <div className="swipe-card-info">
        <div className="swipe-name-row">
          <h2 className="swipe-name">{match.nickname}</h2>
          <span className="swipe-age">{match.age}</span>
          <span className="swipe-city">{match.city}</span>
          <span className="swipe-zodiac">
            <span className="swipe-zodiac-icon">{ZODIAC_ICON[match.zodiac] ?? '✦'}</span>
            {match.zodiac}
          </span>
        </div>
        <p className="swipe-bio">{match.bio}</p>

        <div className="swipe-divider" />

        <p className="swipe-today-label">今天想說的話</p>
        <p className="swipe-today-text">「{match.todaySummary}」</p>

        <div className="swipe-tags">
          {match.interests.map((tag) => (
            <span key={tag} className="swipe-tag">{tag}</span>
          ))}
        </div>

        <p className="swipe-score-row">
          <span className="swipe-score-dot" />
          共鳴指數 {match.matchScore}%
        </p>
      </div>
    </div>
  )
}

function SwipeTutorial({ onStart }: { onStart: () => void }) {
  return (
    <div className="swipe-tutorial">
      <div className="swipe-tutorial-inner">
        <h2 className="swipe-tutorial-title">找找今天同頻的人</h2>
        <p className="swipe-tutorial-sub">根據你今天的心情，我們為你配對了這些人</p>

        {/* SVG 示意圖 */}
        <div className="swipe-tutorial-illustration">
          <svg width="260" height="180" viewBox="0 0 260 180" fill="none" xmlns="http://www.w3.org/2000/svg">
            {/* 中央卡片 */}
            <rect x="80" y="20" width="100" height="130" rx="16" fill="#f5faf8" stroke="#c8e0db" strokeWidth="1.5"/>
            <rect x="92" y="32" width="76" height="44" rx="10" fill="#d4ece6"/>
            <circle cx="108" cy="54" r="10" fill="#b0d8d0" opacity="0.6"/>
            <rect x="92" y="84" width="50" height="6" rx="3" fill="#ddeae8"/>
            <rect x="92" y="96" width="38" height="6" rx="3" fill="#eaf2f0"/>
            <rect x="92" y="108" width="44" height="6" rx="3" fill="#eaf2f0"/>

            {/* 左側飛出卡（跳過）*/}
            <rect x="18" y="32" width="80" height="104" rx="14" fill="#fdf0ef" stroke="#f0c8c4" strokeWidth="1.2" opacity="0.7" transform="rotate(-10 58 84)"/>

            {/* 右側飛出卡（同頻）*/}
            <rect x="162" y="32" width="80" height="104" rx="14" fill="#edf8f4" stroke="#b8ddd6" strokeWidth="1.2" opacity="0.7" transform="rotate(10 202 84)"/>

            {/* 左箭頭 */}
            <g opacity="0.75">
              <path d="M52 158 L28 158" stroke="#dc9090" strokeWidth="2" strokeLinecap="round" markerEnd="url(#arrowL)"/>
              <path d="M34 152 L28 158 L34 164" stroke="#dc9090" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
              <text x="30" y="175" fontSize="11" fill="#dc9090" textAnchor="middle" fontWeight="600">跳過</text>
            </g>

            {/* 右箭頭 */}
            <g opacity="0.75">
              <path d="M208 158 L232 158" stroke="#5fc4a8" strokeWidth="2" strokeLinecap="round"/>
              <path d="M226 152 L232 158 L226 164" stroke="#5fc4a8" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
              <text x="230" y="175" fontSize="11" fill="#5fc4a8" textAnchor="middle" fontWeight="600">同頻</text>
            </g>

            {/* 手指圖示 */}
            <g transform="translate(112, 115)">
              <ellipse cx="8" cy="4" rx="7" ry="4" fill="#c8deda" opacity="0.5"/>
              <rect x="4" y="-12" width="8" height="18" rx="4" fill="#a8c8c4"/>
              <rect x="1" y="-4" width="6" height="14" rx="3" fill="#b8d4d0" transform="rotate(-15 4 3)"/>
              <rect x="10" y="-4" width="6" height="14" rx="3" fill="#b8d4d0" transform="rotate(10 13 3)"/>
            </g>
          </svg>
        </div>

        <div className="swipe-tutorial-tips">
          <div className="swipe-tip">
            <span className="swipe-tip-icon nope">✕</span>
            <span>左滑跳過</span>
          </div>
          <div className="swipe-tip-divider" />
          <div className="swipe-tip">
            <span className="swipe-tip-icon like">♥</span>
            <span>右滑同頻</span>
          </div>
        </div>

        <p className="swipe-tutorial-note">雙方都右滑，才會開啟聊天</p>

        <button className="swipe-tutorial-btn" onClick={onStart}>開始探索</button>
      </div>
    </div>
  )
}

export default function MatchResultPage({ onBack }: Props) {
  const [phase, setPhase] = useState<'loading' | 'tutorial' | 'done'>('loading')
  const [currentIndex, setCurrentIndex] = useState(0)
  const [swipedCount, setSwipedCount] = useState(0)
  const [lastDir, setLastDir] = useState<SwipeDir>(null)
  const matches = FAKE_MATCHES

  useEffect(() => {
    const t = setTimeout(() => {
      const seen = localStorage.getItem(TUTORIAL_KEY)
      setPhase(seen ? 'done' : 'tutorial')
    }, 1800)
    return () => clearTimeout(t)
  }, [])

  function handleSwipe(dir: SwipeDir) {
    setLastDir(dir)
    setSwipedCount((n) => n + 1)
    setCurrentIndex((i) => i + 1)
  }

  function handleActionBtn(dir: SwipeDir) {
    handleSwipe(dir)
  }

  const remaining = matches.length - currentIndex
  const visibleCards = matches.slice(currentIndex, currentIndex + 3)
  const allDone = currentIndex >= matches.length

  return (
    <div className="page match-result-page">
      {phase === 'loading' && (
        <div className="match-loading-mask">
          <div className="match-spinner-ring" />
          <p className="match-loading-text">正在尋找今天同頻的人…</p>
        </div>
      )}

      {phase === 'tutorial' && (
        <SwipeTutorial onStart={() => {
          localStorage.setItem(TUTORIAL_KEY, '1')
          setPhase('done')
        }} />
      )}

      {phase === 'done' && (
        <div className="match-result-content">
          {/* Header */}
          <header className="match-result-header">
            <button className="match-back-btn" onClick={onBack} aria-label="返回">
              <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
                <path d="M12.5 15L7.5 10L12.5 5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              返回
            </button>
            <span className="match-remain-badge">
              {allDone ? '全部看完' : `${remaining} 位`}
            </span>
          </header>

          {/* 卡片區 */}
          {allDone ? (
            <div className="match-empty-state">
              <div className="match-empty-icon">
                <svg width="72" height="72" viewBox="0 0 72 72" fill="none">
                  <circle cx="36" cy="36" r="32" fill="#f0f7f4" />
                  <path d="M24 42c0-6.627 5.373-12 12-12s12 5.373 12 12" stroke="#a8c8c2" strokeWidth="2" strokeLinecap="round" />
                  <circle cx="28" cy="30" r="2.5" fill="#b8d4ce" />
                  <circle cx="44" cy="30" r="2.5" fill="#b8d4ce" />
                </svg>
              </div>
              <p className="match-empty-title">
                {swipedCount > 0 ? '今天的都看完了' : '今天還沒有同頻的人'}
              </p>
              <p className="match-empty-desc">
                {swipedCount > 0
                  ? `你今天滑過了 ${swipedCount} 位，\n已記錄右滑的同頻配對。`
                  : '世界很大，可以明天再來看看。'}
              </p>
              <button className="match-empty-back" onClick={onBack}>回到今日</button>
            </div>
          ) : (
            <>
              <div className="swipe-stack-wrap">
                <div className="swipe-stack">
                  {[...visibleCards].reverse().map((match, i) => {
                    const stackOffset = visibleCards.length - 1 - i
                    const isTop = stackOffset === 0
                    return (
                      <SwipeCard
                        key={match.matchId}
                        match={match}
                        isTop={isTop}
                        stackOffset={stackOffset}
                        onSwipe={isTop ? handleSwipe : () => {}}
                      />
                    )
                  })}
                </div>
              </div>

              {lastDir === 'right' && (
                <p className="swipe-hint">✦ 已標記同頻，等對方也右滑就能聊天</p>
              )}
            </>
          )}
        </div>
      )}
    </div>
  )
}
