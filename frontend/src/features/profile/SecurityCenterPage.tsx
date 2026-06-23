import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { ChevronLeft } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { getBlockedUsers, unblockUser, type BlockedUser } from '../auth/api'

export default function SecurityCenterPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [blocked, setBlocked] = useState<BlockedUser[]>([])
  const [loading, setLoading] = useState(true)
  const [busyId, setBusyId] = useState<string | null>(null)

  const load = useCallback(async () => {
    try {
      const res = await getBlockedUsers()
      setBlocked(res.data)
    } catch { /* ignore */ } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { void load() }, [load])

  async function handleUnblock(blockedId: string) {
    setBusyId(blockedId)
    try {
      await unblockUser(blockedId)
      setBlocked((prev) => prev.filter((b) => b.userId !== blockedId))
    } catch { /* ignore */ } finally {
      setBusyId(null)
    }
  }

  if (!user) return null

  return (
    <div className="page account-page">
      <div className="detail-topbar">
        <button className="detail-back-btn" type="button" onClick={() => navigate('/profile')} aria-label="返回">
          <ChevronLeft size={22} />
        </button>
        <span className="detail-topbar-title">安全中心</span>
      </div>

      <section className="panel" style={{ marginTop: 12 }}>
        <p className="setting-section-title">封鎖名單</p>
        {loading ? (
          <p className="account-danger-note">載入中⋯</p>
        ) : blocked.length === 0 ? (
          <p className="account-danger-note">你還沒有封鎖任何人。</p>
        ) : (
          <ul className="block-list">
            {blocked.map((b) => (
              <li key={b.userId} className="block-item">
                <span className="block-name">{b.nickname}</span>
                <button
                  className="block-unblock-btn"
                  type="button"
                  disabled={busyId === b.userId}
                  onClick={() => handleUnblock(b.userId)}
                >
                  {busyId === b.userId ? '解除中⋯' : '解除封鎖'}
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}
