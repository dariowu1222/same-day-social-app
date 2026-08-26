import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getReports, reviewReport, type ModerationReport, type ReportStatus } from '../api'
import { CATEGORY_LABEL, SeverityBadge, TARGET_LABEL, formatDate } from '../ui'

export default function ReportsPage() {
  const [status, setStatus] = useState<ReportStatus | ''>('PENDING')
  const [items, setItems] = useState<ModerationReport[]>([])
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [loading, setLoading] = useState(false)
  const [openId, setOpenId] = useState<string | null>(null)
  const [resolution, setResolution] = useState('')

  useEffect(() => { load() }, [status])

  async function load() {
    setLoading(true)
    setError('')
    try {
      setItems(await getReports(status))
    } catch (e) {
      setError(e instanceof Error ? e.message : '載入失敗')
    } finally {
      setLoading(false)
    }
  }

  async function review(id: string, next: ReportStatus, hideTarget: boolean | null) {
    setError('')
    try {
      await reviewReport(id, { status: next, resolution: resolution.trim() || undefined, hideTarget })
      setNotice(next === 'DISMISSED' ? '已駁回此檢舉' : '已標記處理完成')
      setOpenId(null)
      setResolution('')
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : '處置失敗')
    }
  }

  return (
    <>
      <div className="page-head">
        <h1 className="page-title">檢舉處理</h1>
        <span className="page-sub">依嚴重度排序，自我傷害與暴力威脅永遠排在最前</span>
      </div>

      <div className="toolbar">
        <select className="select" value={status} onChange={(e) => setStatus(e.target.value as ReportStatus | '')}>
          <option value="PENDING">待處理</option>
          <option value="REVIEWING">處理中</option>
          <option value="RESOLVED">已處理</option>
          <option value="DISMISSED">已駁回</option>
          <option value="">全部</option>
        </select>
        <button className="btn" onClick={load} disabled={loading}>重新整理</button>
      </div>

      {error && <div className="msg msg-error">{error}</div>}
      {notice && <div className="msg msg-ok">{notice}</div>}

      {loading && <div className="empty">載入中…</div>}
      {!loading && items.length === 0 && <div className="empty">目前沒有檢舉</div>}

      <div style={{ display: 'grid', gap: 12 }}>
        {items.map((r) => (
          <div className="card" key={r.id}>
            <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap', marginBottom: 8 }}>
              <SeverityBadge severity={r.severity} />
              <strong>{CATEGORY_LABEL[r.category] ?? r.category}</strong>
              <span className="badge badge-tier">{TARGET_LABEL[r.targetType] ?? r.targetType}</span>
              <span className="mono">{formatDate(r.createdAt)}</span>
              {r.severity >= 100 && (
                <span className="badge badge-sev-high">請優先關心當事人</span>
              )}
            </div>

            {r.detail && (
              <div style={{ marginBottom: 8 }}>
                <div className="stat-label">檢舉人補充</div>
                <div>{r.detail}</div>
              </div>
            )}

            <div style={{ marginBottom: 8 }}>
              <div className="stat-label">被檢舉內容（檢舉當下的快照）</div>
              <div className="snapshot">{r.contextSnapshot || '（無內容）'}</div>
            </div>

            <div className="field-row">
              <span className="field-label">被檢舉人</span>
              <span className="field-value">
                <Link className="link-btn" to={`/users/${r.reportedUserId}`}>{r.reportedUserId}</Link>
                　累計被檢舉 <strong>{r.reportsAgainstUser}</strong> 次
              </span>
            </div>

            {r.status !== 'PENDING' && (
              <div className="field-row">
                <span className="field-label">處理紀錄</span>
                <span className="field-value">
                  <span className="mono">{r.status}　{formatDate(r.reviewedAt)}　by {r.reviewedBy ?? '—'}</span>
                  {r.resolution && <div>{r.resolution}</div>}
                </span>
              </div>
            )}

            {openId === r.id ? (
              <div style={{ marginTop: 10 }}>
                <textarea
                  className="textarea"
                  placeholder="處理說明（會記錄在這筆檢舉上）"
                  value={resolution}
                  onChange={(e) => setResolution(e.target.value)}
                />
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 8 }}>
                  <button className="btn btn-danger" onClick={() => review(r.id, 'RESOLVED', true)}>
                    隱藏內容並結案
                  </button>
                  <button className="btn btn-primary" onClick={() => review(r.id, 'RESOLVED', null)}>
                    不動內容，結案
                  </button>
                  <button className="btn" onClick={() => review(r.id, 'DISMISSED', null)}>
                    駁回檢舉
                  </button>
                  <button className="btn" onClick={() => { setOpenId(null); setResolution('') }}>取消</button>
                </div>
                <div className="page-sub" style={{ marginTop: 6 }}>
                  需要停權或封鎖帳號，請點上方被檢舉人進入其詳細頁處置。
                </div>
              </div>
            ) : (
              <button className="btn btn-sm" style={{ marginTop: 8 }} onClick={() => setOpenId(r.id)}>
                處理這筆
              </button>
            )}
          </div>
        ))}
      </div>
    </>
  )
}
