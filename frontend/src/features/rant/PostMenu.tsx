import { useEffect, useRef, useState } from 'react'
import { MoreVertical, Trash2, Link } from 'lucide-react'

type Props = {
  postId: string
  isOwner: boolean
  onDelete: () => void
}

export default function PostMenu({ postId, isOwner, onDelete }: Props) {
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
