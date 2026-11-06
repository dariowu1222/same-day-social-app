import { useEffect, useRef, useState } from 'react'
// import { useNavigate } from 'react-router-dom' // 任務功能暫時下架（後端保留）
import { Capacitor } from '@capacitor/core'
import {
  Image as ImageIcon, Mic, Square, ArrowUp, MoreHorizontal, X, Search,
  User, Flag, Pencil, Pin, BellOff, BellRing, LogOut, Ban,
  Reply, Copy, RotateCcw, ShieldCheck, AlertTriangle,
} from 'lucide-react'
import type { ChatMessage, ChatRoom as ChatRoomType, ChatMemberSetting } from './types'
import type { QuoteInfo } from './api'
import type { UserProfile } from '../profile/types'
import { getAge, getZodiac, ZODIAC_ICON, testAvatarPhoto } from '../../shared/lib/userDisplay'
import { uploadMedia, uploadErrorMessage } from '../../shared/api/media'
import { useAudioRecorder } from '../../shared/hooks/useAudioRecorder'
import { SAFETY_KEYS, safetyDismissed, dismissSafety } from '../../shared/lib/storageKeys'
import { detectRisks, detectMeetupIntent, detectAbuse, MEETUP_SAFETY_TIPS, type RiskCategory } from './safety'
import Avatar from '../../shared/ui/Avatar'
import ReportSheet from '../report/ReportSheet'
import type { ReportTargetType } from '../report/types'

type Props = {
  room: ChatRoomType
  otherProfile: UserProfile | null
  otherUserId: string
  currentUserId: string
  messages: ChatMessage[]
  draft: string
  onDraftChange: (value: string) => void
  // 實際接的是 async 的 submitContent；宣告回傳 Promise 才能在多圖時依序送出、保住順序
  onSendContent: (content: string, quote?: QuoteInfo) => void | Promise<void>
  onRecall?: (messageId: string) => void
  setting?: ChatMemberSetting
  /** 檢舉已成立（後台認定）：對話唯讀，輸入列換成說明與「退出聊天室」。
      送出檢舉但尚未審核時為 false，兩人照常聊天。 */
  reported?: boolean
  /** 太久沒有往來，房間被排程鎖定：一樣唯讀，只是原因跟檢舉無關。
      鎖定不代表離開，退不退由使用者自己按。 */
  expired?: boolean
  onUpdateSetting?: (patch: { noteName?: string | null; pinned?: boolean; muted?: boolean }) => void
  onLeave?: () => void
  onBlock?: () => void
  onReport?: () => void
  onBack: () => void
}

// 同一群組的相鄰訊息間隔在此秒數內，視為同一段（共用頭像、只在末則標時間）。
const GROUP_GAP_MS = 5 * 60 * 1000

// 自介 sheet 的三段狀態與各自的 translateY 比例（佔 sheet 自身高度，sheet 高度 = 一個螢幕）。
type SheetState = 'closed' | 'peek' | 'full'
const SHEET_TRANSLATE: Record<SheetState, number> = { closed: 1.06, peek: 0.42, full: 0 }

// 一次最多挑幾張圖（與樹洞發文的 MAX_POST_IMAGES 一致）
const MAX_CHAT_IMAGES = 9

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
  messages, draft, onDraftChange, onSendContent, onRecall,
  setting, reported = false, expired = false, onUpdateSetting, onLeave, onBlock, onReport, onBack,
}: Props) {
  // const navigate = useNavigate() // 任務功能暫時下架（後端保留）
  const [sheet, setSheet] = useState<SheetState>('closed')
  const [menuOpen, setMenuOpen] = useState(false)
  const [confirm, setConfirm] = useState<{ title: string; desc: string; danger?: boolean; onYes: () => void } | null>(null)
  const [chatSearch, setChatSearch] = useState<string | null>(null)
  const muted = !!setting?.muted
  const pinned = !!setting?.pinned
  // 檢舉成立與閒置到期都讓房間唯讀，差別只在那段說明文案。
  // 凡是「最後要送出一則訊息」的 UI 都看這個旗標；只跟檢舉本身有關的
  // （選單裡的「檢舉對方」）仍然只看 reported，到期不該把那條路也關掉。
  const readOnly = reported || expired
  const [toast, setToast] = useState('')
  // 多圖上傳中的張數（0 = 沒有在上傳）
  const [uploadingCount, setUploadingCount] = useState(0)

  // 語音訊息：按一下開始錄、再按一下停止，停止後先留在本機讓使用者試聽，
  // 按了傳送才真的上傳。錄壞重錄不會浪費一次上傳，也不會在對話裡留下廢檔。
  const [voiceDraft, setVoiceDraft] = useState<string | null>(null)
  const [sendingVoice, setSendingVoice] = useState(false)
  const { isRecording, toggle: toggleRecording } = useAudioRecorder(setVoiceDraft)

  async function sendVoice() {
    if (!voiceDraft || !onSendContent) return
    setSendingVoice(true)
    try {
      const url = await uploadMedia(voiceDraft, 'chats')
      await onSendContent(url)
      setVoiceDraft(null)
    } catch {
      showToast('語音傳送失敗，請再試一次')
    } finally {
      setSendingVoice(false)
    }
  }
  // 檢舉面板的標的：單則訊息或對方本人
  const [reportTarget, setReportTarget] = useState<{ type: ReportTargetType; id: string } | null>(null)
  // 長按訊息操作列：紀錄目標訊息、歸屬、浮層座標
  const [action, setAction] = useState<{ msg: ChatMessage; isOut: boolean; x: number; y: number } | null>(null)
  // 引用回覆：被引用的訊息
  const [quote, setQuote] = useState<ChatMessage | null>(null)
  // 安全：頂部提示橫幅（全域「不再顯示」）
  const [safetyBannerOpen, setSafetyBannerOpen] = useState(() => !safetyDismissed(SAFETY_KEYS.chatNotice))
  // 安全：約見面提醒 modal（reference=選單點開；pendingSend=送出前自動跳，確認後才送）
  const [meetupModal, setMeetupModal] = useState<{ pendingSend?: () => void } | null>(null)
  const [meetupDontRemind, setMeetupDontRemind] = useState(false)
  const meetupShownThisSession = useRef(false)
  // 安全：送出前風險提示（Review Before Send，不阻擋）
  const [riskDialog, setRiskDialog] = useState<{ categories: RiskCategory[]; onProceed: () => void } | null>(null)
  const [riskMuted, setRiskMuted] = useState(false)
  // 安全：人身攻擊措辭提醒（送出前，不阻擋）
  const [wordingDialog, setWordingDialog] = useState<{ onProceed: () => void } | null>(null)
  const [wordingMuted, setWordingMuted] = useState(false)
  // 檢舉要等真的送出才算數；中途取消什麼都不該發生。
  // 這個旗標的壽命只有一張面板，所以開面板時歸零、關面板時也歸零——兩邊都要。
  // 只靠關閉那一邊不夠：ReportSheet 送出成功後不會自己關，而是換成「已送出」
  // 畫面，這時標的若被換掉（開另一張面板），onClose 根本沒機會跑。
  const reportedRef = useRef(false)
  const toastTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const pressTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const bodyRef = useRef<HTMLDivElement>(null)
  const sheetRef = useRef<HTMLDivElement>(null)
  const fileRef = useRef<HTMLInputElement>(null)
  // App（原生）走上拉拖曳；網頁版走點擊兩段式切換。
  const isNative = Capacitor.isNativePlatform()
  // 拖曳過程的暫存：起點、起始位移(px)、是否真的移動過、最後位移比例。
  const dragRef = useRef<{ startY: number; baseY: number; height: number; moved: boolean; ratio: number } | null>(null)

  const displayName = setting?.noteName?.trim() || otherProfile?.nickname?.trim() || `同頻對話 ${otherUserId.slice(-4)}`
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
      attemptSend()
    }
  }

  function showToast(msg: string) {
    setToast(msg)
    if (toastTimer.current) clearTimeout(toastTimer.current)
    toastTimer.current = setTimeout(() => setToast(''), 2000)
  }

  // ── 更多選單動作 ──
  function openProfile() { setMenuOpen(false); setSheet('peek') }
  // function goTask() { setMenuOpen(false); navigate('/tasks') } // 任務功能暫時下架（後端保留）
  function openChatSearch() { setMenuOpen(false); setChatSearch('') }
  function togglePin() { setMenuOpen(false); onUpdateSetting?.({ pinned: !pinned }); showToast(pinned ? '已取消釘選' : '已釘選對話') }
  function toggleMute() { setMenuOpen(false); onUpdateSetting?.({ muted: !muted }); showToast(muted ? '已開啟通知' : '已靜音通知') }
  function setNote() {
    setMenuOpen(false)
    const next = window.prompt('設定備註名稱（僅自己看得到，清空可移除）', setting?.noteName ?? displayName)
    if (next === null) return
    onUpdateSetting?.({ noteName: next.trim() })
    showToast(next.trim() ? '已設定備註名稱' : '已移除備註名稱')
  }
  function confirmLeave() {
    setMenuOpen(false)
    setConfirm({ title: '離開聊天室', desc: '離開後此對話會從你的列表移除。', danger: true, onYes: () => { setConfirm(null); onLeave ? onLeave() : onBack() } })
  }
  function confirmBlock() {
    setMenuOpen(false)
    setConfirm({ title: '離開並封鎖對方', desc: '封鎖後對方無法再與你聊天，此對話也會移除。', danger: true, onYes: () => { setConfirm(null); onBlock ? onBlock() : onBack() } })
  }
  function confirmReport() {
    setMenuOpen(false)
    setConfirm({ title: '檢舉對方', desc: '接著會請你選擇原因。我們會盡快查看，處理完成會通知你；在那之前你們仍然可以繼續對話。', danger: true, onYes: () => { setConfirm(null); reportedRef.current = false; setReportTarget({ type: 'USER', id: otherUserId }) } })
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
  function actReport() {
    const target = action?.msg.id
    setAction(null)
    if (target) {
      reportedRef.current = false
      setReportTarget({ type: 'CHAT_MESSAGE', id: target })
    }
  }
  function actRecall() {
    if (action) onRecall?.(action.msg.id)
    setAction(null)
  }

  // 實際送出（含引用條時帶上引用資訊）
  function doSend() {
    const text = draft.trim()
    if (!text) return
    // 只帶引用哪一則。名字與內容由伺服器查原訊息決定——這三欄以前整組由這裡填，
    // 彼此之間沒有對帳，可以做出一句對方從來沒說過的話。
    const q: QuoteInfo | undefined = quote ? { quotedMessageId: quote.id } : undefined
    onSendContent?.(text, q)
    onDraftChange('')
    setQuote(null)
  }

  // 送出前安全攔截：最多跳一個（風險優先、其次約見），都不阻擋、確認後照送。
  function attemptSend() {
    const text = draft.trim()
    if (!text) return

    if (detectAbuse(text) && !wordingMuted) {
      setWordingDialog({ onProceed: doSend })
      return
    }

    const risks = detectRisks(text)
    if (risks.length > 0 && !riskMuted) {
      setRiskDialog({ categories: risks, onProceed: doSend })
      return
    }

    if (detectMeetupIntent(text) && !safetyDismissed(SAFETY_KEYS.meetup) && !meetupShownThisSession.current) {
      meetupShownThisSession.current = true
      setMeetupDontRemind(false)
      setMeetupModal({ pendingSend: doSend })
      return
    }

    doSend()
  }

  // 約見面安全提醒：選單點開（純參考）
  function openMeetupSafety() {
    setMenuOpen(false)
    setMeetupDontRemind(false)
    setMeetupModal({})
  }

  function closeMeetupModal() {
    if (meetupDontRemind) dismissSafety(SAFETY_KEYS.meetup)
    const pending = meetupModal?.pendingSend
    setMeetupModal(null)
    pending?.()
  }

  function dismissSafetyBanner() {
    dismissSafety(SAFETY_KEYS.chatNotice)
    setSafetyBannerOpen(false)
  }

  // 正在輸入時，上方那條「回覆 OOO」。手上就是原訊息本身。
  function quoteName(msg: ChatMessage) {
    return msg.senderId === currentUserId ? '你' : displayName
  }

  // 已送出的泡泡裡那條引用。伺服器存的是發話者當時的暱稱，但引用自己說過的話
  // 寫「你」比寫自己的名字自然，所以在還載得到原訊息時換掉。
  function quotedName(msg: ChatMessage) {
    const quoted = messages.find((m) => m.id === msg.quotedMessageId)
    return quoted?.senderId === currentUserId ? '你' : msg.quotedSenderName
  }
  function quotePreview(content: string) {
    const t = content.trim()
    if (isImageMedia(t)) return '圖片'
    if (isAudioMedia(t)) return '語音'
    return content
  }

  // 圖片訊息：縮圖並上傳後，以圖片網址當訊息內容送出（泡泡會偵測圖片網址並渲染）
  // 多張時＝多則訊息（與 LINE 一致）：上傳平行加速，送出依序以保住選取順序。
  async function handlePickImage(event: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? []).slice(0, MAX_CHAT_IMAGES)
    event.target.value = ''
    if (files.length === 0 || !onSendContent) return

    setUploadingCount(files.length)
    try {
      const urls = await Promise.all(
        files.map(async (file) => uploadMedia(await imageToDataUrl(file), 'chats')),
      )
      for (const url of urls) {
        await onSendContent(url)
      }
    } catch (err) {
      // 以前 uploadMedia 失敗會悄悄退回整串 base64，照常「送出」成功，
      // 於是一張幾百 KB 的圖被當成訊息內容寫進資料庫。
      showToast(uploadErrorMessage(err))
    } finally {
      setUploadingCount(0)
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
        <button className="cr-menu" type="button" onClick={() => setMenuOpen(true)} aria-label="更多">
          <MoreHorizontal size={19} strokeWidth={2} />
        </button>
      </header>

      {/* 安全提示橫幅：進聊天室提醒，可永久關閉 */}
      {safetyBannerOpen && (
        <div className="cr-safety-banner">
          <ShieldCheck size={16} strokeWidth={2} className="cr-safety-banner-ic" />
          <span className="cr-safety-banner-text">注意安全：不要匯款或透露金融資訊，個資與聯絡方式等信任後再分享。</span>
          <button className="cr-safety-banner-close" type="button" onClick={dismissSafetyBanner}>我知道了，不再顯示</button>
        </div>
      )}

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
                      <span className="cr-quote-block-name">{quotedName(row.message)}</span>
                      <span className="cr-quote-block-text">{quotePreview(row.message.quotedContent)}</span>
                    </div>
                  )}
                  {row.message.isRecalled
                    ? <span className="cr-recalled">訊息已收回</span>
                    : renderContent(row.message.content)}
                </div>
                {/* 樂觀更新的送出狀態：送達後伺服器版本沒有 status，標記自動消失 */}
                {row.message.status === 'sending' && <span className="cr-msg-status">傳送中…</span>}
                {row.message.status === 'failed' && <span className="cr-msg-status failed">傳送失敗</span>}
                {row.isGroupEnd && row.message.status == null && (
                  <span className="cr-time">{formatTime(row.message.createdAt)}</span>
                )}
              </div>
            </div>
          ),
        )}
      </div>

      {/* 引用回覆條。它是送出流程的一部分，所以兩種唯讀狀態都要收掉 */}
      {quote && !readOnly && (
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

      {/* 多圖上傳中的提示：張數多時要讓使用者知道還在處理 */}
      {uploadingCount > 0 && (
        <div className="cr-upload-hint">
          正在傳送 {uploadingCount} 張圖片…
        </div>
      )}

      {isRecording && (
        <div className="cr-upload-hint cr-recording-hint">
          <span className="cr-recording-dot" aria-hidden="true" />
          錄音中…再按一次麥克風停止
        </div>
      )}

      {/* 錄好之後先試聽，確認了才送。按 X 可以丟掉重錄。
          唯讀時收掉：這一塊在輸入列外面，不跟著 cr-frozen 一起被換掉。
          房間在錄好、還沒按傳送的空檔被鎖（重抓 overview 時才會發現），
          留著那顆傳送鈕只會讓人按下去拿到一個失敗。 */}
      {voiceDraft && !isRecording && !readOnly && (
        <div className="cr-voice-draft">
          <audio controls src={voiceDraft} className="cr-voice-draft-audio" />
          <button
            className="cr-voice-draft-btn discard"
            type="button"
            onClick={() => setVoiceDraft(null)}
            disabled={sendingVoice}
            aria-label="刪除錄音"
          >
            <X size={15} strokeWidth={2.2} />
          </button>
          <button
            className="cr-voice-draft-btn send"
            type="button"
            onClick={sendVoice}
            disabled={sendingVoice}
          >
            {sendingVoice ? '傳送中…' : '傳送'}
          </button>
        </div>
      )}

      {/* 唯讀狀態取代輸入列，兩種原因共用這一塊、只換說明：
          檢舉成立的觸發點是後台認定，不是送出檢舉——按送出就關對話的話，
          一次誤報就能把別人的對話鎖死；閒置到期則是排程只負責鎖，不代為退出。
          兩者都不把房間移除：使用者常常還要回頭看對方說過什麼，而且對話
          憑空消失會讓人以為自己誤觸。退不退由他自己決定。
          同時成立時講檢舉——那個比較嚴重，也比較需要交代。 */}
      {readOnly ? (
        <div className="cr-frozen">
          {reported ? (
            <>
              <p className="cr-frozen-title">你檢舉的對象已經被處置。</p>
              <p className="cr-frozen-desc">這段對話已經關閉，先前的訊息還看得到，但不能再傳送。</p>
            </>
          ) : (
            <>
              <p className="cr-frozen-title">太久沒有往來，這段對話已經關閉。</p>
              <p className="cr-frozen-desc">先前的訊息都還在，可以慢慢回頭看，只是不能再傳送了。</p>
            </>
          )}
          <button className="cr-frozen-leave" type="button" onClick={confirmLeave}>
            <LogOut size={17} strokeWidth={2} />退出聊天室
          </button>
        </div>
      ) : (
      <div className="cr-input">
        <button
          className="cr-input-icon cr-img"
          type="button"
          onClick={() => fileRef.current?.click()}
          disabled={uploadingCount > 0}
          aria-label="傳送圖片"
        >
          <ImageIcon size={20} strokeWidth={1.9} />
        </button>
        <button
          className={`cr-input-icon cr-voice${isRecording ? ' recording' : ''}`}
          type="button"
          onClick={toggleRecording}
          disabled={sendingVoice || uploadingCount > 0 || (!!voiceDraft && !isRecording)}
          aria-label={isRecording ? '停止錄音' : '語音訊息'}
          aria-pressed={isRecording}
        >
          {isRecording ? <Square size={16} strokeWidth={2.4} /> : <Mic size={20} strokeWidth={1.9} />}
        </button>
        <input
          className="cr-input-box"
          value={draft}
          onChange={(event) => onDraftChange(event.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="慢慢說，不急。"
        />
        <button className="cr-send" type="button" onClick={attemptSend} disabled={!draft.trim()} aria-label="送出">
          <ArrowUp size={18} strokeWidth={2.4} />
        </button>
        <input ref={fileRef} type="file" accept="image/*" multiple hidden onChange={handlePickImage} />
      </div>
      )}

      {/* 更多選單 action sheet */}
      <div className={`cr-menu-mask${menuOpen ? ' show' : ''}`} onClick={() => setMenuOpen(false)} />
      <div className={`cr-menu-sheet${menuOpen ? ' show' : ''}`} role="dialog" aria-label="更多選項">
        <div className="cr-menu-grip" />
        <div className="cr-menu-section-label">互動 / 設定</div>
        <button className="cr-menu-item" type="button" onClick={openProfile}>
          <User size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>查看資料</span>
        </button>
        {/* 任務功能暫時下架（後端保留）
        <button className="cr-menu-item" type="button" onClick={goTask}>
          <Flag size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>一起做任務</span>
          <span className="cr-menu-badge">招牌</span>
        </button>
        */}
        <button className="cr-menu-item" type="button" onClick={openChatSearch}>
          <Search size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>搜尋對話內容</span>
        </button>
        <button className="cr-menu-item" type="button" onClick={openMeetupSafety}>
          <ShieldCheck size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>約見面安全提醒</span>
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
        {/* 已經處置過就不再給：再送一次對審核沒有幫助，而使用者按下去只會
            看到同一張面板，像是上一次沒成功。
            這裡刻意不看 readOnly——閒置到期只是不能再傳訊息，對方先前說過
            的話仍然該檢舉得了。 */}
        {!reported && (
          <button className="cr-menu-item danger" type="button" onClick={confirmReport}>
            <Flag size={19} strokeWidth={1.8} className="cr-menu-ic" /><span>檢舉對方</span>
          </button>
        )}
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

      {/* 檢舉面板：單則訊息、或對方本人（送出後畫面不變，要等後台認定成立才唯讀） */}
      {reportTarget && (
        <ReportSheet
          targetType={reportTarget.type}
          targetId={reportTarget.id}
          title={reportTarget.type === 'USER' ? '檢舉對方' : '檢舉這則訊息'}
          onClose={() => {
            const wasUserReport = reportTarget.type === 'USER'
            // 不管這次檢舉的是訊息還是人，旗標的壽命都只到這張面板關上為止。
            // 先前只有 USER 那一支會歸零，於是「檢舉一則訊息 → 送出」之後旗標
            // 一直留著 true，接下來開「檢舉對方」再按取消，這裡會誤判成送出過，
            // 替使用者補送一筆 POST /api/chats/{id}/report——他明明按的是取消。
            const didSubmit = reportedRef.current
            reportedRef.current = false
            setReportTarget(null)
            if (wasUserReport && didSubmit) {
              onReport ? onReport() : onBack()
            }
          }}
          onDone={() => { reportedRef.current = true }}
        />
      )}

      {/* 約見面安全提醒 modal */}
      {meetupModal && (
        <div className="cr-safety-mask" onClick={() => { if (!meetupModal.pendingSend) closeMeetupModal() }}>
          <div className="cr-safety-modal" role="dialog" aria-label="約見面安全提醒" onClick={(e) => e.stopPropagation()}>
            <div className="cr-safety-head">
              <ShieldCheck size={20} strokeWidth={2} /><span>約見面安全提醒</span>
            </div>
            <ul className="cr-safety-list">
              {MEETUP_SAFETY_TIPS.map((t) => <li key={t}>{t}</li>)}
            </ul>
            <label className="cr-safety-check">
              <input type="checkbox" checked={meetupDontRemind} onChange={(e) => setMeetupDontRemind(e.target.checked)} />
              <span>我了解，不要再自動提醒</span>
            </label>
            <button className="cr-safety-primary" type="button" onClick={closeMeetupModal}>
              {meetupModal.pendingSend ? '了解，傳送訊息' : '我知道了'}
            </button>
          </div>
        </div>
      )}

      {/* 送出前風險提示（不阻擋） */}
      {riskDialog && (
        <div className="cr-safety-mask" onClick={() => setRiskDialog(null)}>
          <div className="cr-safety-modal" role="alertdialog" aria-label="傳送前確認" onClick={(e) => e.stopPropagation()}>
            <div className="cr-safety-head warn">
              <AlertTriangle size={20} strokeWidth={2} /><span>傳送前確認</span>
            </div>
            <p className="cr-safety-desc">
              這則訊息可能包含<strong>{riskDialog.categories.join('、')}</strong>。為了你的安全，請先確認對方值得信任——你仍然可以選擇傳送。
            </p>
            <label className="cr-safety-check">
              <input type="checkbox" onChange={(e) => setRiskMuted(e.target.checked)} />
              <span>這次對話不再提醒</span>
            </label>
            <div className="cr-safety-actions">
              <button className="cr-safety-secondary" type="button" onClick={() => setRiskDialog(null)}>再想想</button>
              <button className="cr-safety-primary" type="button" onClick={() => { const proceed = riskDialog.onProceed; setRiskDialog(null); proceed() }}>仍要傳送</button>
            </div>
          </div>
        </div>
      )}

      {/* 送出前措辭提醒（人身攻擊，不阻擋） */}
      {wordingDialog && (
        <div className="cr-safety-mask" onClick={() => setWordingDialog(null)}>
          <div className="cr-safety-modal" role="alertdialog" aria-label="措辭提醒" onClick={(e) => e.stopPropagation()}>
            <div className="cr-safety-head warn">
              <AlertTriangle size={20} strokeWidth={2} /><span>換個說法？</span>
            </div>
            <p className="cr-safety-desc">
              這則訊息可能帶有攻擊或辱罵字眼。好好說話更容易被理解——你仍然可以照原樣傳送。
            </p>
            <label className="cr-safety-check">
              <input type="checkbox" onChange={(e) => setWordingMuted(e.target.checked)} />
              <span>這次對話不再提醒</span>
            </label>
            <div className="cr-safety-actions">
              <button className="cr-safety-secondary" type="button" onClick={() => setWordingDialog(null)}>再想想</button>
              <button className="cr-safety-primary" type="button" onClick={() => { const proceed = wordingDialog.onProceed; setWordingDialog(null); proceed() }}>仍要傳送</button>
            </div>
          </div>
        </div>
      )}

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
            {/* 引用回覆結尾是送出一則訊息，唯讀就收掉；底下的複製與檢舉
                都不送東西出去，兩種唯讀狀態都留著 */}
            {!readOnly && (
              <button className="cr-action-btn" type="button" onClick={actReply}>
                <Reply size={17} strokeWidth={1.9} /><span>回覆</span>
              </button>
            )}
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

// 偵測媒體：支援 data URL 與物件儲存的 http(s) 圖片/音訊 URL
function isImageMedia(s: string) {
  return /^data:image\//.test(s) || (/^https?:\/\//.test(s) && /\.(jpe?g|png|webp|gif)(\?|$)/i.test(s))
}
function isAudioMedia(s: string) {
  return /^data:audio\//.test(s) || (/^https?:\/\//.test(s) && /\.(mp3|webm|wav|m4a|ogg)(\?|$)/i.test(s))
}

function renderContent(content: string) {
  const trimmed = content.trim()
  if (isImageMedia(trimmed)) return <img src={trimmed} className="cr-bubble-img" alt="圖片" />
  if (isAudioMedia(trimmed)) return <audio controls src={trimmed} className="cr-bubble-audio" />
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
