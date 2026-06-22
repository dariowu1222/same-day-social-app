import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ChevronLeft } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { deleteAccount } from '../auth/api'

export default function AccountCenterPage() {
  const navigate = useNavigate()
  const { user, logout } = useAuth()
  const [confirming, setConfirming] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  if (!user) return null

  async function handleDelete() {
    setBusy(true)
    setError('')
    try {
      await deleteAccount()
      logout() // 清 token + user → 導回登入頁
    } catch (e) {
      setError(e instanceof Error ? e.message : '刪除失敗，請稍後再試。')
      setBusy(false)
    }
  }

  return (
    <div className="page account-page">
      <div className="detail-topbar">
        <button className="detail-back-btn" type="button" onClick={() => navigate('/profile')} aria-label="返回">
          <ChevronLeft size={22} />
        </button>
        <span className="detail-topbar-title">帳號中心</span>
      </div>

      <section className="panel" style={{ marginTop: 12 }}>
        <p className="setting-section-title">帳號</p>
        <div className="account-row"><span>暱稱</span><strong>{user.nickname}</strong></div>
        {user.email && <div className="account-row"><span>Email</span><strong>{user.email}</strong></div>}
      </section>

      <section className="panel">
        <p className="setting-section-title">危險操作</p>
        <p className="account-danger-note">
          刪除帳號後會停用登入，並清除你的個人資料與照片。此動作目前無法自行復原。
        </p>
        <button className="account-delete-btn" type="button" onClick={() => setConfirming(true)}>
          刪除帳號
        </button>
        {error && <p className="account-error">{error}</p>}
      </section>

      {confirming && (
        <div className="cr-confirm-mask" onClick={() => !busy && setConfirming(false)}>
          <div className="cr-confirm" role="alertdialog" onClick={e => e.stopPropagation()}>
            <div className="cr-confirm-title">確定要刪除帳號？</div>
            <div className="cr-confirm-desc">登入會被停用、個人資料與照片會被清除。確定要繼續嗎？</div>
            <div className="cr-confirm-actions">
              <button className="cr-confirm-cancel" type="button" disabled={busy} onClick={() => setConfirming(false)}>取消</button>
              <button className="cr-confirm-ok danger" type="button" disabled={busy} onClick={handleDelete}>
                {busy ? '刪除中⋯' : '確定刪除'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
