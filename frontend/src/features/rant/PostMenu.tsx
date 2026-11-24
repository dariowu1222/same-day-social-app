import { useEffect, useRef, useState } from 'react'
import { MoreVertical, Trash2, Link, Flag } from 'lucide-react'

type Props = {
  postId: string
  isOwner: boolean
  onDelete: () => void
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

  function handleDelete() {
    setOpen(false)
    onDelete()
  }

  return (
    <div className="post-menu-wrap" ref={menuRef}>
      <button className="post-menu-trigger" onClick={() => setOpen((v) => !v)} title="更多">
        <MoreVertical size={18} />
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

      {toast && <div className="post-menu-toast">{toast}</div>}
    </div>
  )
}
