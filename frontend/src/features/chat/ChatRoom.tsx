import { useEffect, useRef, useState } from 'react'
import { Capacitor } from '@capacitor/core'
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
  messages, draft, onDraftChange, onSend, onBack,
}: Props) {
  const [sheet, setSheet] = useState<SheetState>('closed')
  const bodyRef = useRef<HTMLDivElement>(null)
  const sheetRef = useRef<HTMLDivElement>(null)
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

  const rows = buildRows(messages, currentUserId)

  // 訊息更新後捲到底
  useEffect(() => {
    const el = bodyRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [messages.length, room.id])

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter' && !event.nativeEvent.isComposing && draft.trim()) {
      event.preventDefault()
      onSend()
    }
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
        <button className="cr-menu" type="button" aria-label="更多">⋯</button>
      </header>

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
                <div className="cr-bubble">{renderContent(row.message.content)}</div>
                {row.isGroupEnd && (
                  <span className="cr-time">{formatTime(row.message.createdAt)}</span>
                )}
              </div>
            </div>
          ),
        )}
      </div>

      {/* 輸入列 */}
      <div className="cr-input">
        <input
          className="cr-input-box"
          value={draft}
          onChange={(event) => onDraftChange(event.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="慢慢說，不急。"
        />
        <button className="cr-send" type="button" onClick={onSend} disabled={!draft.trim()} aria-label="送出">↑</button>
      </div>

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
  if (trimmed === '[image]') return '🖼️ 圖片'
  if (trimmed === '[sticker]') return '😊 貼圖'
  return content
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
