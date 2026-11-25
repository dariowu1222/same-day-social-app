import { useEffect, useLayoutEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Check, MoreHorizontal } from 'lucide-react'

export type ActionMenuItem = {
  key: string
  label: string
  icon?: ReactNode
  /** 會造成損失的動作（收回、刪除、檢舉）：紅字 */
  danger?: boolean
  /** 現在不能做。配合 hint 說明為什麼，不要整項拿掉——拿掉了使用者只會以為沒有這個功能 */
  disabled?: boolean
  /** 標籤底下的一行小字 */
  hint?: string
  /** 單選清單用：目前選到的這一項前面打勾 */
  checked?: boolean
  /** 這一項上面畫一條分隔線 */
  separated?: boolean
  onSelect?: () => void
}

type Props = {
  items: ActionMenuItem[]
  /** 給讀屏的名稱，也是預設那顆「⋯」的 title */
  label?: string
  /** 不給就是一顆「⋯」。給了就用它當按鈕的內容（例如排序那顆上面的字） */
  trigger?: ReactNode
  className?: string
}

/**
 * 點一下展開的功能選單。回應右上角的「⋯」、回應排序都用它。
 *
 * 選單本身畫在 body 底下（portal）並用 fixed 定位，不是畫在按鈕旁邊：
 * 樹洞卡片的回應區有 overflow: clip（展開收合的動畫需要），選單如果是按鈕的
 * 子元素，靠近底部的那幾則一打開就會被切掉。
 *
 * 位置在打開的那一刻量：預設往下開、靠按鈕的右邊對齊；下面放不下就往上開。
 * 按鈕被捲走就關掉——選單是釘在螢幕上的，內容捲走之後它會指著別的東西。
 */
export default function ActionMenu({ items, label = '更多', trigger, className }: Props) {
  const [open, setOpen] = useState(false)
  const [style, setStyle] = useState<CSSProperties>({ visibility: 'hidden' })
  const buttonRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)

  useLayoutEffect(() => {
    if (!open) return
    const button = buttonRef.current
    const menu = menuRef.current
    if (!button || !menu) return
    const at = button.getBoundingClientRect()
    const height = menu.offsetHeight
    const width = menu.offsetWidth
    const gap = 6
    const below = at.bottom + gap + height <= window.innerHeight - 8
    // 靠右對齊按鈕；太靠左邊（選單會超出畫面）就貼著畫面左緣
    const left = Math.max(8, Math.min(at.right - width, window.innerWidth - width - 8))
    setStyle({
      left,
      top: below ? at.bottom + gap : Math.max(8, at.top - gap - height),
      visibility: 'visible',
    })
  }, [open, items.length])

  useEffect(() => {
    if (!open) return
    const close = () => setOpen(false)
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node
      if (menuRef.current?.contains(target) || buttonRef.current?.contains(target)) return
      close()
    }
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape') close() }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    window.addEventListener('resize', close)
    // 捲動：按鈕在螢幕上「真的移開了」才關，不是有捲動事件就關。
    //
    // 會送出捲動事件、但按鈕其實沒動的情況很多：上面某張圖片載入完把內容撐高，
    // 瀏覽器為了不讓畫面跳而自己補了捲動量；或是按下去的那一刻慣性捲動剛好還沒停。
    // 這些時候選單指著的還是同一顆按鈕，關掉只會讓人覺得「怎麼點了又不見」
    // （實測在真資料上就遇到了）。capture：捲的是裡面某個容器也要收得到。
    const openedAt = buttonRef.current?.getBoundingClientRect()
    const onScroll = () => {
      const now = buttonRef.current?.getBoundingClientRect()
      if (!openedAt || !now || Math.abs(now.top - openedAt.top) > 8 || Math.abs(now.left - openedAt.left) > 8) close()
    }
    window.addEventListener('scroll', onScroll, true)
    return () => {
      window.removeEventListener('scroll', onScroll, true)
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
      window.removeEventListener('resize', close)
    }
  }, [open])

  if (items.length === 0) return null

  function toggle() {
    // 每次打開都從「看不見」開始，等位置量好才顯示，不會先在錯的位置閃一下
    if (!open) setStyle({ visibility: 'hidden' })
    setOpen((value) => !value)
  }

  return (
    <>
      <button
        ref={buttonRef}
        type="button"
        className={`action-menu-trigger${trigger ? ' has-label' : ''}${className ? ` ${className}` : ''}`}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={trigger ? undefined : label}
        title={trigger ? undefined : label}
        onClick={toggle}
      >
        {trigger ?? <MoreHorizontal size={18} aria-hidden="true" />}
      </button>

      {open && createPortal(
        <div ref={menuRef} className="action-menu" role="menu" aria-label={label} style={style}>
          {items.map((item) => (
            <button
              key={item.key}
              type="button"
              role={item.checked === undefined ? 'menuitem' : 'menuitemradio'}
              aria-checked={item.checked}
              className={`action-menu-item${item.danger ? ' is-danger' : ''}${item.separated ? ' is-separated' : ''}`}
              disabled={item.disabled}
              onClick={() => { setOpen(false); item.onSelect?.() }}
            >
              <span className="action-menu-text">
                <span className="action-menu-label">{item.label}</span>
                {item.hint && <span className="action-menu-hint">{item.hint}</span>}
              </span>
              {item.checked !== undefined
                ? <span className="action-menu-icon">{item.checked && <Check size={16} strokeWidth={2.6} aria-hidden="true" />}</span>
                : item.icon && <span className="action-menu-icon" aria-hidden="true">{item.icon}</span>}
            </button>
          ))}
        </div>,
        document.body,
      )}
    </>
  )
}
