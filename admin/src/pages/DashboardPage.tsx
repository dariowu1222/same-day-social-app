import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getDashboard, type Dashboard } from '../api'

export default function DashboardPage() {
  const [data, setData] = useState<Dashboard | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    getDashboard()
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : '載入失敗'))
  }, [])

  return (
    <>
      <div className="page-head">
        <h1 className="page-title">總覽</h1>
        <span className="page-sub">開啟本頁時會一併讓到期的停權自動失效</span>
      </div>

      {error && <div className="msg msg-error">{error}</div>}
      {!data && !error && <div className="empty">載入中…</div>}

      {data && (
        <>
          <div className="stat-grid">
            <div className="card">
              <div className="stat-label">會員總數</div>
              <div className="stat-value">{data.totalUsers}</div>
            </div>
            <div className="card">
              <div className="stat-label">今日新增</div>
              <div className="stat-value">{data.newUsersToday}</div>
            </div>
            <div className="card">
              <div className="stat-label">停權中</div>
              <div className={`stat-value${data.suspendedUsers > 0 ? ' warn' : ''}`}>{data.suspendedUsers}</div>
            </div>
            <div className="card">
              <div className="stat-label">已封鎖</div>
              <div className={`stat-value${data.bannedUsers > 0 ? ' danger' : ''}`}>{data.bannedUsers}</div>
            </div>
          </div>

          <div className="stat-grid">
            <div className="card">
              <div className="stat-label">待處理檢舉</div>
              <div className={`stat-value${data.pendingReports > 0 ? ' warn' : ''}`}>{data.pendingReports}</div>
              <Link className="link-btn" to="/reports">前往處理</Link>
            </div>
            <div className="card">
              <div className="stat-label">其中高嚴重度</div>
              <div className={`stat-value${data.highSeverityReports > 0 ? ' danger' : ''}`}>
                {data.highSeverityReports}
              </div>
              <span className="page-sub">自我傷害、暴力威脅等，請優先處理</span>
            </div>
          </div>
        </>
      )}
    </>
  )
}
