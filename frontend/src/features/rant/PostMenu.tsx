import { useEffect, useRef, useState } from 'react'
import { MoreHorizontal, Trash2, Link, Flag } from 'lucide-react'
import ConfirmDialog from '../../shared/ui/ConfirmDialog'

type Props = {
  postId: string
  isOwner: boolean
  /** 使用者在確認視窗按下「刪除」之後才會被呼叫。回傳 Promise 的話視窗會等它，失敗就把原因顯示出來 */
  onDelete: () => Promise<unknown> | void
  /**
   * 有給就在選單裡多一項「檢舉這則」。
   *
   * 檢舉原本是卡片動作列上一顆紅字按鈕，跟愛心、回應排在一起——每一張卡片都有，
   * 整面牆於是到處是紅字，而那是一年按不到幾次的功能。收進選單之後它還在，
   * 只是不再搶在「我懂」和「回應」前面。自己的貼文不顯示（自己檢舉自己沒有意義）。
   */
  onReport?: () => void
}

export default function PostMenu({ postId, isOwner, onDelete, onReport }: Props) {
  const [open, setOpen] = useState(false)
  const [toast, setToast] = useState('')
  // 確認視窗開著沒
  const [confirming, setConfirming] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  function copyLink() {
    const base = import.meta.env.VITE_APP_BASE_URL ?? window.location.origin
    const url = `${base}/#/rant/${postId}`
    navigator.clipboard.writeText(url).then(() => {
      setToast('已複製連結')
      setTimeout(() => setToast(''), 2000)
    }).catch(() => {
      setToast('複製失敗')
      setTimeout(() => setToast(''), 2000)
    })
    setOpen(false)
  }

  /**
   * 刪除先跳確認視窗（參考 Threads）。
   *
   * 最早是一按就刪，沒有任何確認；後來改成同一項按兩下，但那要看得懂「字變了」
   * 是在問你。現在刪掉之後底下的回應會留著（照 Threads 的做法），這件事要在
   * 按下去之前講清楚——不然作者會以為整串都沒了，之後才發現帶著連結的人還看得到。
   */
  function handleDelete() {
    setOpen(false)
    setConfirming(true)
  }

  return (
    <div className="post-menu-wrap" ref={menuRef}>
      <button className="post-menu-trigger" onClick={() => setOpen((v) => !v)} title="更多">
        <MoreHorizontal size={18} />
      </button>

      {open && (
        <div className="post-menu-dropdown">
          <button className="post-menu-item" onClick={copyLink}>
            <Link size={15} /> 複製連結
          </button>
          {onReport && !isOwner && (
            <button className="post-menu-item" onClick={() => { setOpen(false); onReport() }}>
              <Flag size={15} /> 檢舉這則
            </button>
          )}
          {isOwner && (
            <button className="post-menu-item danger" onClick={handleDelete}>
              <Trash2 size={15} /> 刪除貼文
            </button>
          )}
        </div>
      )}

      {confirming && (
        <ConfirmDialog
          title="刪除貼文？"
          description="刪掉之後拿不回來。底下的回應會留著，只是看不到你這篇"
          confirmLabel="刪除"
          danger
          onConfirm={onDelete}
          onClose={() => setConfirming(false)}
        />
      )}

      {toast && <div className="post-menu-toast">{toast}</div>}
    </div>
  )
}
