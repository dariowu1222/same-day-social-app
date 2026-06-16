import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Capacitor } from '@capacitor/core'
import {
  Image as ImageIcon, Mic, ArrowUp, MoreHorizontal, X, Search,
  User, Flag, Pencil, Pin, BellOff, BellRing, LogOut, Ban,
  Reply, Copy, RotateCcw,
} from 'lucide-react'
import type { ChatMessage, ChatRoom as ChatRoomType } from './types'
import type { UserProfile } from '../profile/types'
import { getAge, getZodiac, ZODIAC_ICON, testAvatarPhoto } from '../../shared/lib/userDisplay'
import Avatar from '../../shared/ui/Avatar'

type Props = {
  room: ChatRoomType
  otherProfile: UserProfile | null
  otherUserId: string
  currentUserId: string
  messages: ChatMessage[]
  draft: string
  onDraftChange: (value: string) => void
  onSend: () => void
  onSendContent?: (content: string) => void
  onBack: () => void
}

// 同一群組的相鄰訊息間隔在此秒數內，視為同一段（共用頭像、只在末則標時間）。
const GROUP_GAP_MS = 5 * 60 * 1000

// 自介 sheet 的三段狀態與各自的 translateY 比例（佔 sheet 自身高度，sheet 高度 = 一個螢幕）。
type SheetState = 'closed' | 'peek' | 'full'
const SHEET_TRANSLATE: Record<SheetState, number> = { closed: 1.06, peek: 0.42, full: 0 }

type RenderRow =
  | { kind: 'date'; key: string; label: string }
  | {
      kind: 'msg'
      key: string
      message: ChatMessage
      isOut: boolean
      isGroupStart: boolean
      isGroupEnd: boolean
    }

export default function ChatRoom({
  room, otherProfile, otherUserId, currentUserId,
  messages, draft, onDraftChange, onSend, onSendContent, onBack,
}: Props) {
  const navigate = useNavigate()
  const [sheet, setSheet] = useState<SheetState>('closed')
  const [menuOpen, setMenuOpen] = useState(false)
  const [confirm, setConfirm] = useState<{ title: string; desc: string; danger?: boolean; onYes: () => void } | null>(null)
  const [chatSearch, setChatSearch] = useState<string | null>(null)
  const [muted, setMuted] = useState(false)
  const [pinned, setPinned] = useState(false)
  const [toast, setToast] = useState('')
  // 長按訊息操作列：紀錄目標訊息、歸屬、浮層座標
  const [action, setAction] = useState<{ msg: ChatMessage; isOut: boolean; x: number; y: number } | null>(null)
  // 引用回覆：被引用的訊息
  const [quote, setQuote] = useState<ChatMessage | null>(null)
  const toastTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const pressTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const bodyRef = useRef<HTMLDivElement>(null)
  const sheetRef = useRef<HTMLDivElement>(null)
  const fileRef = useRef<HTMLInputElement>(null)
  // App（原生）走上拉拖曳；網頁版走點擊兩段式切換。
  const isNative = Capacitor.isNativePlatform()
  // 拖曳過程的暫存：起點、起始位移(px)、是否真的移動過、最後位移比例。
  const dragRef = useRef<{ startY: number; baseY: number; height: number; moved: boolean; ratio: number } | null>(null)

  const displayName = otherProfile?.nickname?.trim() || `同頻對話 ${otherUserId.slice(-4)}`
  // 正式環境會改用帳號實際 photoDataUrls[0]；目前測試先給寫死照片，照片失效時 Avatar 會退回動物頭像。
  const avatarPhoto = otherProfile?.photoDataUrls?.[0] ?? testAvatarPhoto(otherUserId)
  const birthday = otherProfile?.birthday
  const subtitle = birthday
    ? `${getAge(birthday)} · ${getZodiac(birthday)}`
    : '輕觸看 TA 的介紹'

  const shownMessages = chatSearch
    ? messages.filter((m) => m.content.toLowerCase().includes(chatSearch.toLowerCase()))
    : messages
  const rows = buildRows(shownMessages, currentUserId)

  // 訊息更新後捲到底
  useEffect(() => {
    const el = bodyRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [messages.length, room.id])

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter' && !event.nativeEvent.isComposing && draft.trim()) {
      event.preventDefault()
      handleSend()
    }
  }

  function showToast(msg: string) {
    setToast(msg)
    if (toastTimer.current) clearTimeout(toastTimer.current)
    toastTimer.current = setTimeout(() => setToast(''), 2000)
  }

  // ── 更多選單動作 ──
  function openProfile() { setMenuOpen(false); setSheet('peek') }
  function goTask() { setMenuOpen(false); navigate('/tasks') }
  function openChatSearch() { setMenuOpen(false); setChatSearch('') }
  function togglePin() { setMenuOpen(false); setPinned((v) => !v); showToast(pinned ? '已取消釘選' : '已釘選對話') }
  function toggleMute() { setMenuOpen(false); setMuted((v) => !v); showToast(muted ? '已開啟通知' : '已靜音通知') }
  function setNote() { setMenuOpen(false); showToast('備註名稱將在後端批次接通') }
  // 離開類：三項都先二次確認（封鎖/檢舉的後端在 Task #5 接通）
  function confirmLeave() {
    setMenuOpen(false)
    setConfirm({ title: '離開聊天室', desc: '離開後此對話會從你的列表移除。', danger: true, onYes: () => { setConfirm(null); onBack() } })
  }
  function confirmBlock() {
    setMenuOpen(false)
    setConfirm({ title: '離開並封鎖對方', desc: '封鎖後對方無法再與你聊天，此對話也會移除。', danger: true, onYes: () => { setConfirm(null); showToast('封鎖將在後端批次接通'); onBack() } })
  }
  function confirmReport() {
    setMenuOpen(false)
    setConfirm({ title: '離開並檢舉對方', desc: '我們會收到你的檢舉並審核，此對話也會移除。', danger: true, onYes: () => { setConfirm(null); showToast('檢舉將在後端批次接通'); onBack() } })
  }

  // ── 長按 / 右鍵訊息 → 操作列 ──
  function openAction(node: HTMLElement, msg: ChatMessage, isOut: boolean) {
    const rect = node.getBoundingClientRect()
    setAction({ msg, isOut, x: isOut ? rect.right : rect.left, y: rect.top })
  }
  function onBubbleContextMenu(e: React.MouseEvent, msg: ChatMessage, isOut: boolean) {
    e.preventDefault()
    openAction(e.currentTarget as HTMLElement, msg, isOut)
  }
  function onBubbleTouchStart(e: React.TouchEvent, msg: ChatMessage, isOut: boolean) {
    const node = e.currentTarget as HTMLElement
    pressTimer.current = setTimeout(() => openAction(node, msg, isOut), 450)
  }
  function clearPress() {
    if (pressTimer.current) clearTimeout(pressTimer.current)
  }
  // 收回限本人發出後 2 分鐘內
  function canRecall(msg: ChatMessage) {
    return Date.now() - new Date(msg.createdAt).getTime() < 2 * 60 * 1000
  }
  function actReply() { if (action) { setQuote(action.msg); setAction(null) } }
  function actCopy() {
    if (!action) return
    navigator.clipboard?.writeText(action.msg.content).catch(() => {})
    setAction(null)
    showToast('已複製')
  }
  function actReport() { setAction(null); showToast('檢舉將在後端批次接通') }
  function actRecall() { setAction(null); showToast('收回將在後端批次接通') }

  // 送出時清掉引用條（引用關聯的後端串接在 Task #4）
  function handleSend() {
    onSend()
    setQuote(null)
  }

  function quoteName(msg: ChatMessage) {
    return msg.senderId === currentUserId ? '你' : displayName
  }
  function quotePreview(content: string) {
    const t = content.trim()
    if (t.startsWith('data:image/')) return '圖片'
    if (t.startsWith('data:audio/')) return '語音'
    return content
  }

  // 圖片訊息：縮圖後以 data URL 直接當訊息內容送出（泡泡會偵測 data:image 並渲染圖片）
  async function handlePickImage(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file || !onSendContent) return
    const dataUrl = await imageToDataUrl(file)
    onSendContent(dataUrl)
  }

  // 把手被觸/點：網頁版直接 peek↔full 切換；原生版交給拖曳處理（tap 才在 touchend 切換）。
  function toggleSheet() {
    setSheet((s) => (s === 'full' ? 'peek' : 'full'))
  }

  // ── 原生：把手上拉/下拉拖曳，跟手移動、放開時 snap 到最近段 ──
  function onGrabTouchStart(event: React.TouchEvent) {
    if (!isNative) return
    const height = sheetRef.current?.offsetHeight ?? window.innerHeight
    dragRef.current = {
      startY: event.touches[0].clientY,
      baseY: SHEET_TRANSLATE[sheet] * height,
      height,
      moved: false,
      ratio: SHEET_TRANSLATE[sheet],
    }
    if (sheetRef.current) sheetRef.current.style.transition = 'none'
  }

  function onGrabTouchMove(event: React.TouchEvent) {
    const d = dragRef.current
    if (!d) return
    const delta = event.touches[0].clientY - d.startY
    if (Math.abs(delta) > 4) d.moved = true
    const y = Math.max(0, Math.min(d.baseY + delta, d.height)) // 夾在 全屏(0)～關閉(整屏高)
    d.ratio = y / d.height
    if (sheetRef.current) sheetRef.current.style.transform = `translateY(${y}px)`
  }

  function onGrabTouchEnd() {
    const d = dragRef.current
    if (!d) return
    dragRef.current = null
    if (sheetRef.current) {
      sheetRef.current.style.transition = ''
      sheetRef.current.style.transform = '' // 交還給狀態 class 決定位置
    }
    if (!d.moved) {
      toggleSheet() // 沒移動 = 點擊，當作展開/收合
      return
    }
    // 依放開時所在比例 snap：靠上→全屏、中段→半截、靠下→關閉
    setSheet(d.ratio < 0.2 ? 'full' : d.ratio < 0.72 ? 'peek' : 'closed')
  }

  return (
    <div className="page cr-page">
      {/* 精簡標題列 */}
      <header className="cr-header">
        <button className="cr-back" type="button" onClick={onBack} aria-label="返回">‹</button>
        <button className="cr-header-id" type="button" onClick={() => setSheet('peek')}>
          <Avatar photo={avatarPhoto} seed={otherUserId} className="cr-header-avatar" />
          <span className="cr-header-text">
            <span className="cr-header-name">{displayName}</span>
            <span className="cr-header-sub">{subtitle}</span>
          </span>
        </button>
        <button className="cr-menu" type="button" onClick={() => setMenuOpen(true)} aria-label="更多">
          <MoreHorizontal size={19} strokeWidth={2} />
        </button>
      </header>

      {/* 站內搜尋對話內容 */}
      {chatSearch !== null && (
        <div className="cr-search-bar">
          <Search size={14} className="cr-search-icon" strokeWidth={2} />
          <input
            className="cr-search-input"
            value={chatSearch}
            onChange={(e) => setChatSearch(e.target.value)}
            placeholder="搜尋對話內容"
            autoFocus
          />
          <button className="cr-search-close" type="button" onClick={() => setChatSearch(null)} aria-label="關閉搜尋">
            <X size={14} strokeWidth={2} />
          </button>
        </div>
      )}

      {/* 訊息區 */}
      <div className="cr-body" ref={bodyRef}>
        {rows.length === 0 && (
          <p className="cr-empty">還沒有訊息，慢慢說，不急。</p>
        )}
        {rows.map((row) =>
          row.kind === 'date' ? (
            <div key={row.key} className="cr-date">{row.label}</div>
          ) : (
            <div
              key={row.key}
              className={`cr-row ${row.isOut ? 'out' : 'in'}${row.isGroupStart ? '' : ' mid'}`}
            >
              {!row.isOut && (
                row.isGroupStart ? (
                  <button className="cr-row-avatar-btn" type="button" onClick={() => setSheet('peek')} aria-label="看介紹">
                    <Avatar photo={avatarPhoto} seed={otherUserId} className="cr-row-avatar" />
                  </button>
                ) : (
                  <span className="cr-row-avatar spacer" />
                )
              )}
              <div className="cr-bubble-wrap">
                <div
                  className="cr-bubble"
                  onContextMenu={(e) => onBubbleContextMenu(e, row.message, row.isOut)}
                  onTouchStart={(e) => onBubbleTouchStart(e, row.message, row.isOut)}
                  onTouchEnd={clearPress}
                  onTouchMove={clearPress}
                >
                  {row.message.quotedContent && (
                    <div className={`cr-quote-block${row.isOut ? ' out' : ''}`}>
                      <span className="cr-quote-block-name">{row.message.quotedSenderName}</span>
                      <span className="cr-quote-block-text">{row.message.quotedContent}</span>
                    </div>
                  )}
                  {row.message.isRecalled
                    ? <span className="cr-recalled">訊息已收回</span>
                    : renderContent(row.message.content)}
                </div>
                {row.isGroupEnd && (
                  <span className="cr-time">{formatTime(row.message.createdAt)}</span>
                )}
              </div>
            </div>
          ),
        )}
      </div>

      {/* 引用回覆條 */}
      {quote && (
        <div className="cr-quote-bar">
          <div className="cr-quote-bar-body">
            <span className="cr-quote-bar-name">回覆 {quoteName(quote)}</span>
            <span className="cr-quote-bar-text">{quotePreview(quote.content)}</span>
          </div>
          <button className="cr-quote-bar-close" type="button" onClick={() => setQuote(null)} aria-label="取消回覆">
            <X size={14} strokeWidth={2} />
          </button>
        </div>
      )}

      {/* 輸入列：圖片 / 語音 / 輸入框 / 送出 */}
      <div className="cr-input">
        <button className="cr-input-icon cr-img" type="button" onClick={() => fileRef.current?.click()} aria-label="傳送圖片">
          <ImageIcon size={20} strokeWidth={1.9} />
        </button>
        <button className="cr-input-icon cr-voice" type="button" aria-label="語音訊息">
          <Mic size={20} strokeWidth={1.9} />
        </button>
        <input
          className="cr-input-box"
          value={draft}
          onChange={(event) => onDraftChange(event.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="慢慢說，不急。"
        />
        <button className="cr-send" type="button" onClick={handleSend} disabled={!draft.trim()} aria-label="送出">
          <ArrowUp size={18} strokeWidth={2.4} />
        </button>
        <input ref={fileRef} type="file" accept="image/*" hidden onChange={handlePickImage} />
      </div>

      {/* 更多選單 action sheet */}
      <div className={`cr-menu-mask${menuOpen ? ' show' : ''}`} onClick={() => setMenuOpen(false)} />
      <div className={`cr-menu-sheet${menuOpen ? ' show' : ''}`} role="dialog" aria-label="更多選項">
        <div className="cr-menu-grip" />
        <div className="cr-menu-section-label">互動 / 設定</div>
        <button className="cr-menu-item" type="button" onClick={openProfile}>
          <User size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>查看資料</span>
        </button>
        <button className="cr-menu-item" type="button" onClick={goTask}>
          <Flag size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>一起做任務</span>
          <span className="cr-menu-badge">招牌</span>
        </button>
        <button className="cr-menu-item" type="button" onClick={openChatSearch}>
          <Search size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>搜尋對話內容</span>
        </button>
        <button className="cr-menu-item" type="button" onClick={setNote}>
          <Pencil size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>設定備註名稱</span>
        </button>
        <button className="cr-menu-item" type="button" onClick={togglePin}>
          <Pin size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>{pinned ? '取消釘選' : '釘選對話'}</span>
        </button>
        <button className="cr-menu-item" type="button" onClick={toggleMute}>
          {muted
            ? <BellRing size={19} strokeWidth={1.8} className="cr-menu-ic" />
            : <BellOff size={19} strokeWidth={1.8} className="cr-menu-ic" />}
          <span>{muted ? '開啟通知' : '靜音通知'}</span>
        </button>
        <div className="cr-menu-divider" />
        <div className="cr-menu-section-label">離開</div>
        <button className="cr-menu-item danger" type="button" onClick={confirmLeave}>
          <LogOut size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>離開聊天室</span>
        </button>
        <button className="cr-menu-item danger" type="button" onClick={confirmBlock}>
          <Ban size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>離開並封鎖對方</span>
        </button>
        <button className="cr-menu-item danger" type="button" onClick={confirmReport}>
          <Flag size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>離開並檢舉對方</span>
        </button>
      </div>

      {/* 離開類動作二次確認 */}
      {confirm && (
        <div className="cr-confirm-mask" onClick={() => setConfirm(null)}>
          <div className="cr-confirm" role="alertdialog" onClick={(e) => e.stopPropagation()}>
            <div className="cr-confirm-title">{confirm.title}</div>
            <div className="cr-confirm-desc">{confirm.desc}</div>
            <div className="cr-confirm-actions">
              <button className="cr-confirm-cancel" type="button" onClick={() => setConfirm(null)}>取消</button>
              <button className={`cr-confirm-ok${confirm.danger ? ' danger' : ''}`} type="button" onClick={confirm.onYes}>確認</button>
            </div>
          </div>
        </div>
      )}

      {toast && <div className="cr-toast">{toast}</div>}

      {/* 長按訊息操作列 */}
      {action && (
        <>
          <div
            className="cr-action-mask"
            onClick={() => setAction(null)}
            onContextMenu={(e) => { e.preventDefault(); setAction(null) }}
          />
          <div
            className={`cr-action-bar${action.isOut ? ' out' : ''}`}
            style={{ left: action.x, top: action.y }}
          >
            <button className="cr-action-btn" type="button" onClick={actReply}>
              <Reply size={17} strokeWidth={1.9} /><span>回覆</span>
            </button>
            <button className="cr-action-btn" type="button" onClick={actCopy}>
              <Copy size={17} strokeWidth={1.9} /><span>複製</span>
            </button>
            {action.isOut
              ? (canRecall(action.msg) && (
                  <button className="cr-action-btn" type="button" onClick={actRecall}>
                    <RotateCcw size={17} strokeWidth={1.9} /><span>收回</span>
                  </button>
                ))
              : (
                  <button className="cr-action-btn" type="button" onClick={actReport}>
                    <Flag size={17} strokeWidth={1.9} /><span>檢舉</span>
                  </button>
                )}
          </div>
        </>
      )}

      {/* 自介 Sheet：closed → peek（半截）→ full（全屏） */}
      <div
        className={`cr-sheet-mask${sheet !== 'closed' ? ' show' : ''}`}
        onClick={() => setSheet('closed')}
      />
      <div
        ref={sheetRef}
        className={`cr-sheet cr-sheet--${sheet}`}
        role="dialog"
        aria-label={`${displayName} 的介紹`}
      >
        {/* 把手列：原生可拖曳、網頁可點擊；全屏時左上顯示返回鍵 */}
        <div
          className="cr-sheet-grab"
          onClick={isNative ? undefined : toggleSheet}
          onTouchStart={onGrabTouchStart}
          onTouchMove={onGrabTouchMove}
          onTouchEnd={onGrabTouchEnd}
        >
          {sheet === 'full' && (
            <button
              className="cr-sheet-back"
              type="button"
              onClick={(e) => { e.stopPropagation(); setSheet('closed') }}
              aria-label="返回聊天室"
            >‹ 返回</button>
          )}
          <div className="cr-sheet-handle" />
          {sheet === 'peek' && (
            <div className="cr-sheet-hint">{isNative ? '上拉看完整介紹' : '點此展開完整介紹'}</div>
          )}
        </div>

        <div className="cr-sheet-body">
          <Avatar photo={avatarPhoto} seed={otherUserId} className="cr-bio-avatar" />
          <div className="cr-bio-name">{displayName}</div>
          {birthday && (
            <div className="cr-bio-meta">
              {ZODIAC_ICON[getZodiac(birthday)] ?? '✦'} {getZodiac(birthday)} · {getAge(birthday)} 歲
            </div>
          )}

          {(otherProfile?.interestTags?.length || otherProfile?.valueTags?.length) ? (
            <div className="cr-bio-tags">
              {otherProfile?.interestTags?.map((tag) => (
                <span key={`i-${tag}`} className="cr-bio-tag">{tag}</span>
              ))}
              {otherProfile?.valueTags?.map((tag) => (
                <span key={`v-${tag}`} className="cr-bio-tag value">{tag}</span>
              ))}
            </div>
          ) : null}

          <div className="cr-bio-label">自我介紹</div>
          <div className="cr-bio-text">
            {otherProfile?.bio?.trim()
              ? otherProfile.bio
              : <span className="cr-bio-muted">這個人還沒寫自我介紹。</span>}
          </div>
        </div>
      </div>
    </div>
  )
}

// 把訊息攤平成「日期分隔 + 訊息」的渲染列，並標註每則是否為群組首／末則。
function buildRows(messages: ChatMessage[], currentUserId: string): RenderRow[] {
  const rows: RenderRow[] = []
  let lastDateKey = ''

  messages.forEach((message, index) => {
    const date = new Date(message.createdAt)
    const dateKey = `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`
    if (dateKey !== lastDateKey) {
      rows.push({ kind: 'date', key: `date-${dateKey}-${message.id}`, label: formatDateLabel(date) })
      lastDateKey = dateKey
    }

    const prev = messages[index - 1]
    const next = messages[index + 1]
    const isOut = message.senderId === currentUserId

    const startsNewDate = !prev || dateKeyOf(prev) !== dateKey
    const isGroupStart = startsNewDate
      || prev!.senderId !== message.senderId
      || new Date(message.createdAt).getTime() - new Date(prev!.createdAt).getTime() > GROUP_GAP_MS

    const nextSameDate = next && dateKeyOf(next) === dateKey
    const isGroupEnd = !next
      || !nextSameDate
      || next!.senderId !== message.senderId
      || new Date(next!.createdAt).getTime() - new Date(message.createdAt).getTime() > GROUP_GAP_MS

    rows.push({ kind: 'msg', key: message.id, message, isOut, isGroupStart, isGroupEnd })
  })

  return rows
}

function dateKeyOf(message: ChatMessage) {
  const d = new Date(message.createdAt)
  return `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`
}

function renderContent(content: string) {
  const trimmed = content.trim()
  if (trimmed.startsWith('data:image/')) return <img src={trimmed} className="cr-bubble-img" alt="圖片" />
  if (trimmed.startsWith('data:audio/')) return <audio controls src={trimmed} className="cr-bubble-audio" />
  if (trimmed === '[image]') return '🖼️ 圖片'
  if (trimmed === '[sticker]') return '😊 貼圖'
  return content
}

// 圖片縮圖 → data URL（與個人頁同樣的 canvas 縮放，控制體積）
function imageToDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const img = new Image()
    const url = URL.createObjectURL(file)
    img.onload = () => {
      URL.revokeObjectURL(url)
      const maxSize = 800
      const scale = Math.min(1, maxSize / Math.max(img.width, img.height))
      const w = Math.round(img.width * scale)
      const h = Math.round(img.height * scale)
      const canvas = document.createElement('canvas')
      canvas.width = w
      canvas.height = h
      canvas.getContext('2d')!.drawImage(img, 0, 0, w, h)
      resolve(canvas.toDataURL('image/jpeg', 0.82))
    }
    img.onerror = reject
    img.src = url
  })
}

function formatTime(value: string) {
  const d = new Date(value)
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

function formatDateLabel(date: Date) {
  const now = new Date()
  const sameDay = date.getFullYear() === now.getFullYear()
    && date.getMonth() === now.getMonth()
    && date.getDate() === now.getDate()
  if (sameDay) return '今天'

  const yesterday = new Date(now)
  yesterday.setDate(now.getDate() - 1)
  const isYesterday = date.getFullYear() === yesterday.getFullYear()
    && date.getMonth() === yesterday.getMonth()
    && date.getDate() === yesterday.getDate()
  if (isYesterday) return '昨天'

  return `${date.getMonth() + 1}月${date.getDate()}日`
}
