import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { searchUsers, type AccountStatus, type PagedUsers } from '../api'
import { Pagination, StatusBadge, formatDate } from '../ui'

const PAGE_SIZE_OPTIONS = [20, 50, 100]

export default function UsersPage() {
  const [keyword, setKeyword] = useState('')
  const [status, setStatus] = useState<AccountStatus | ''>('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(PAGE_SIZE_OPTIONS[0])
  const [data, setData] = useState<PagedUsers | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  // keyword 不放依賴：改成按下搜尋才查，避免每打一個字就打一次 API
  useEffect(() => {
    load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [status, page, pageSize])

  async function load() {
    setLoading(true)
    setError('')
    try {
      setData(await searchUsers({ keyword, status, page, pageSize }))
    } catch (e) {
      setError(e instanceof Error ? e.message : '載入失敗')
    } finally {
      setLoading(false)
    }
  }

  function submitSearch(e: React.FormEvent) {
    e.preventDefault()
    if (page !== 1) setPage(1)
    else load()
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.total / pageSize)) : 1

  return (
    <>
      <div className="page-head">
        <h1 className="page-title">會員管理</h1>
        {data && <span className="page-sub">共 {data.total} 位</span>}
      </div>

      <form className="toolbar" onSubmit={submitSearch}>
        <input
          className="input"
          placeholder="搜尋暱稱，或貼上完整 ID"
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
        />
        <select
          className="select"
          value={status}
          onChange={(e) => { setStatus(e.target.value as AccountStatus | ''); setPage(1) }}
        >
          <option value="">全部狀態</option>
          <option value="ACTIVE">正常</option>
          <option value="SUSPENDED">停權中</option>
          <option value="BANNED">已封鎖</option>
          <option value="DELETED">已註銷</option>
        </select>
        <button className="btn btn-primary" disabled={loading}>搜尋</button>
        <select
          className="select"
          style={{ minWidth: 110 }}
          value={pageSize}
          onChange={(e) => { setPageSize(Number(e.target.value)); setPage(1) }}
        >
          {PAGE_SIZE_OPTIONS.map((n) => <option key={n} value={n}>每頁 {n} 筆</option>)}
        </select>
      </form>

      {error && <div className="msg msg-error">{error}</div>}

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>暱稱</th>
              <th>狀態</th>
              <th>等級</th>
              <th>驗證</th>
              <th>被檢舉</th>
              <th>最後登入</th>
              <th>註冊時間</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {data?.items.map((u) => (
              <tr key={u.id}>
                <td>
                  <div>{u.nickname}</div>
                  <div className="mono">{u.id}</div>
                </td>
                <td>
                  <StatusBadge status={u.status} />
                  {u.suspendedUntil && (
                    <div className="mono">至 {formatDate(u.suspendedUntil)}</div>
                  )}
                </td>
                <td><span className="badge badge-tier">{u.membershipTier}</span></td>
                <td className="mono">
                  {u.emailVerified ? '信箱✓' : '—'}{u.identityVerified ? ' 實名✓' : ''}
                </td>
                <td>{u.reportsAgainst > 0 ? <strong>{u.reportsAgainst}</strong> : '0'}</td>
                <td className="mono">{u.lastLoginAt ? formatDate(u.lastLoginAt) : '未曾登入'}</td>
                <td className="mono">{formatDate(u.createdAt)}</td>
                <td><Link className="link-btn" to={`/users/${u.id}`}>詳細</Link></td>
              </tr>
            ))}
          </tbody>
        </table>

        {!loading && data?.items.length === 0 && <div className="empty">沒有符合條件的會員</div>}
        {loading && <div className="empty">載入中…</div>}
      </div>

      {data && (
        <div className="pager">
          <Pagination page={page} totalPages={totalPages} onChange={setPage} />
          {totalPages > 1 && (
            <span className="pager-info">第 {page} / {totalPages} 頁，共 {data.total} 位</span>
          )}
        </div>
      )}
    </>
  )
}
