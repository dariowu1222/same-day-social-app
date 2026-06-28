import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { ChevronLeft, Heart, Sprout, Bell } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { getNotifications, markAllNotificationsRead, NOTIFICATION_PAGE_SIZE } from './api'
import type { AppNotification } from './types'

function relativeTime(iso: string): string {
  const then = new Date(iso).getTime()
  if (Number.isNaN(then)) return ''
  const diff = Date.now() - then
  const min = Math.floor(diff / 60000)
  if (min < 1) return '剛剛'
  if (min < 60) return `${min} 分鐘前`
  const hr = Math.floor(min / 60)
  if (hr < 24) return `${hr} 小時前`
  const day = Math.floor(hr / 24)
  if (day < 7) return `${day} 天前`
  return new Date(then).toLocaleDateString()
}

function typeIcon(type: string) {
  if (type === 'LIKE') return <Heart size={18} strokeWidth={2} />
  if (type === 'RANT_REPLY') return <Sprout size={18} strokeWidth={2} />
  return <Bell size={18} strokeWidth={2} />
}

export default function NotificationCenterPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [items, setItems] = useState<AppNotification[]>([])
  const [loading, setLoading] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const [hasMore, setHasMore] = useState(false)

  const load = useCallback(async () => {
    try {
      const res = await getNotifications()
      setItems(res.data)
      setHasMore(res.data.length === NOTIFICATION_PAGE_SIZE)
      // 開啟通知中心即全部標記已讀（紅點隨之清除）
      void markAllNotificationsRead().catch(() => {})
    } catch { /* ignore */ } finally {
      setLoading(false)
    }
  }, [])
  useEffect(() => { void load() }, [load])

  async function loadMore() {
    if (loadingMore || items.length === 0) return
    setLoadingMore(true)
    try {
      const cursor = items[items.length - 1].createdAt
      const res = await getNotifications(cursor)
      setItems(prev => [...prev, ...res.data])
      setHasMore(res.data.length === NOTIFICATION_PAGE_SIZE)
    } catch { /* ignore */ } finally {
      setLoadingMore(false)
    }
  }

  function open(n: AppNotification) {
    if (n.linkType === 'rant' && n.linkId) navigate(`/rant/${n.linkId}`)
    else navigate('/')
  }

  if (!user) return null

  return (
    <div className="page account-page">
      <div className="detail-topbar">
        <button className="detail-back-btn" type="button" onClick={() => navigate('/profile')} aria-label="返回">
          <ChevronLeft size={22} />
        </button>
        <span className="detail-topbar-title">通知中心</span>
      </div>

      {!loading && items.length === 0 && (
        <div className="notif-empty">
          <Bell size={32} strokeWidth={1.4} />
          <p>還沒有通知</p>
          <span>有人對你有共鳴、或回應你的樹洞時，會出現在這裡。</span>
        </div>
      )}

      {items.length > 0 && (
        <section className="panel" style={{ marginTop: 12 }}>
          {items.map(n => (
            <button
              key={n.id}
              type="button"
              className={`notif-row${n.isRead ? '' : ' unread'}`}
              onClick={() => open(n)}
            >
              <span className={`notif-icon notif-icon-${n.type.toLowerCase()}`}>{typeIcon(n.type)}</span>
              <span className="notif-body">
                <span className="notif-title">{n.title}</span>
                {n.body && <span className="notif-text">{n.body}</span>}
                <span className="notif-time">{relativeTime(n.createdAt)}</span>
              </span>
              {!n.isRead && <span className="notif-dot" aria-label="未讀" />}
            </button>
          ))}
        </section>
      )}

      {hasMore && (
        <button className="notif-more-btn" type="button" disabled={loadingMore} onClick={loadMore}>
          {loadingMore ? '載入中⋯' : '載入更多'}
        </button>
      )}
    </div>
  )
}
