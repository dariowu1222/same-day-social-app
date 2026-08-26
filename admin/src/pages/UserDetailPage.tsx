import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  getUserDetail, sanctionUser, updateUser,
  type MembershipTier, type SanctionAction, type UserDetail,
} from '../api'
import { CATEGORY_LABEL, REPORT_STATUS_LABEL, SANCTION_LABEL, SeverityBadge, StatusBadge, TARGET_LABEL, formatDate } from '../ui'

export default function UserDetailPage() {
  const { userId = '' } = useParams()
  const [user, setUser] = useState<UserDetail | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  // 編輯欄位
  const [nickname, setNickname] = useState('')
  const [bio, setBio] = useState('')
  const [tier, setTier] = useState<MembershipTier>('FREE')

  // 處置欄位
  const [reason, setReason] = useState('')
  const [days, setDays] = useState('7')

  useEffect(() => { load() }, [userId])

  async function load() {
    setError('')
    try {
      const data = await getUserDetail(userId)
      setUser(data)
      setNickname(data.nickname)
      setBio(data.bio)
      setTier(data.membershipTier)
    } catch (e) {
      setError(e instanceof Error ? e.message : '載入失敗')
    }
  }

  async function saveProfile() {
    if (busy) return
    setBusy(true)
    setError('')
    try {
      await updateUser(userId, { nickname, bio, membershipTier: tier })
      setNotice('資料已更新')
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : '更新失敗')
    } finally {
      setBusy(false)
    }
  }

  async function doSanction(action: SanctionAction) {
    if (busy) return

    // 停權以外的處置都是重手，且不會自動失效，先確認一次
    const confirmText: Partial<Record<SanctionAction, string>> = {
      BAN: '確定要永久封鎖這個帳號？對方將立即無法登入。',
      DELETE: '確定要註銷這個帳號？對方將無法登入，資料會保留供爭議查證。',
    }
    const ask = confirmText[action]
    if (ask && !window.confirm(ask)) return

    setBusy(true)
    setError('')
    try {
      const expiresAt = action === 'SUSPEND' && Number(days) > 0
        ? new Date(Date.now() + Number(days) * 86400000).toISOString()
        : null
      await sanctionUser(userId, { action, reason: reason.trim() || undefined, expiresAt })
      setNotice(`已執行：${SANCTION_LABEL[action] ?? action}`)
      setReason('')
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : '處置失敗')
    } finally {
      setBusy(false)
    }
  }

  if (error && !user) return <div className="msg msg-error">{error}</div>
  if (!user) return <div className="empty">載入中…</div>

  const isActive = user.status === 'ACTIVE'

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">{user.nickname}</h1>
          <div className="mono">{user.id}</div>
        </div>
        <Link className="link-btn" to="/users">← 回會員列表</Link>
      </div>

      {error && <div className="msg msg-error">{error}</div>}
      {notice && <div className="msg msg-ok">{notice}</div>}

      <div className="detail-grid">
        {/* 左：基本資料與編輯 */}
        <div style={{ display: 'grid', gap: 16 }}>
          <div className="card">
            <h2 className="section-title">帳號資訊</h2>
            <div className="field-row">
              <span className="field-label">狀態</span>
              <span className="field-value">
                <StatusBadge status={user.status} />
                {user.suspendedUntil && <span className="mono"> 至 {formatDate(user.suspendedUntil)}</span>}
                {user.statusReason && <div className="page-sub">原因：{user.statusReason}</div>}
              </span>
            </div>
            <div className="field-row">
              <span className="field-label">登入帳號</span>
              <span className="field-value">{user.username ?? '（無，demo 帳號）'}</span>
            </div>
            <div className="field-row">
              <span className="field-label">驗證</span>
              <span className="field-value">
                信箱 {user.emailVerified ? '✓' : '✗'}　手機 {user.phoneVerified ? '✓' : '✗'}　實名 {user.identityVerified ? '✓' : '✗'}
              </span>
            </div>
            <div className="field-row">
              <span className="field-label">註冊 / 登入</span>
              <span className="field-value mono">{formatDate(user.createdAt)}　最後登入 {formatDate(user.lastLoginAt)}</span>
            </div>
            <div className="field-row">
              <span className="field-label">活動量</span>
              <span className="field-value">
                貼文 {user.postCount}　回應 {user.replyCount}　訊息 {user.messageCount}
                被檢舉 <strong>{user.reportsAgainst}</strong>
              </span>
            </div>
            <div className="field-row">
              <span className="field-label">基本資料</span>
              <span className="field-value">
                {[user.gender, user.ageRange, user.locationArea, user.occupation, user.school]
                  .filter(Boolean).join('・') || '—'}
              </span>
            </div>
          </div>

          <div className="card">
            <h2 className="section-title">編輯資料</h2>
            <div style={{ display: 'grid', gap: 10 }}>
              <label>
                <div className="stat-label">暱稱</div>
                <input className="input" style={{ width: '100%' }} value={nickname} onChange={(e) => setNickname(e.target.value)} />
              </label>
              <label>
                <div className="stat-label">自我介紹</div>
                <textarea className="textarea" value={bio} onChange={(e) => setBio(e.target.value)} />
              </label>
              <label>
                <div className="stat-label">會員等級（付費機制尚未開放，僅先記錄）</div>
                <select className="select" value={tier} onChange={(e) => setTier(e.target.value as MembershipTier)}>
                  <option value="FREE">FREE</option>
                  <option value="PREMIUM">PREMIUM</option>
                  <option value="VIP">VIP</option>
                </select>
              </label>
              <div>
                <button className="btn btn-primary" onClick={saveProfile} disabled={busy}>儲存變更</button>
              </div>
            </div>
          </div>
        </div>

        {/* 右：處置與歷史 */}
        <div style={{ display: 'grid', gap: 16 }}>
          <div className="card">
            <h2 className="section-title">帳號處置</h2>
            <label>
              <div className="stat-label">原因（會記入處置歷史）</div>
              <textarea className="textarea" value={reason} onChange={(e) => setReason(e.target.value)} />
            </label>

            {isActive ? (
              <>
                <label style={{ display: 'block', margin: '10px 0' }}>
                  <div className="stat-label">停權天數（0 表示不設到期日，需人工解除）</div>
                  <input className="input" type="number" min={0} value={days} onChange={(e) => setDays(e.target.value)} />
                </label>
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  <button className="btn" onClick={() => doSanction('SUSPEND')} disabled={busy}>停權</button>
                  <button className="btn btn-danger" onClick={() => doSanction('BAN')} disabled={busy}>永久封鎖</button>
                  <button className="btn btn-danger" onClick={() => doSanction('DELETE')} disabled={busy}>註銷帳號</button>
                </div>
              </>
            ) : (
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 10 }}>
                {user.status === 'SUSPENDED' && (
                  <button className="btn btn-primary" onClick={() => doSanction('UNSUSPEND')} disabled={busy}>解除停權</button>
                )}
                {user.status === 'BANNED' && (
                  <button className="btn btn-primary" onClick={() => doSanction('UNBAN')} disabled={busy}>解除封鎖</button>
                )}
                {user.status === 'DELETED' && (
                  <span className="page-sub">此帳號已註銷。如需復原，請解除後另行處理。</span>
                )}
              </div>
            )}
          </div>

          <div className="card">
            <h2 className="section-title">
              被檢舉紀錄
              {user.reports.length > 0 && <span className="page-sub">（{user.reports.length} 筆，含已駁回）</span>}
            </h2>
            {user.reports.length === 0 && <div className="page-sub">尚無檢舉</div>}
            {user.reports.map((r) => (
              <div className="report-entry" key={r.id}>
                <div className="report-entry-head">
                  <SeverityBadge severity={r.severity} />
                  <strong>{CATEGORY_LABEL[r.category] ?? r.category}</strong>
                  <span className="badge badge-tier">{TARGET_LABEL[r.targetType] ?? r.targetType}</span>
                  <span className={`badge ${r.status === 'PENDING' ? 'badge-suspended' : 'badge-deleted'}`}>
                    {REPORT_STATUS_LABEL[r.status] ?? r.status}
                  </span>
                </div>
                <div className="mono">{formatDate(r.createdAt)}</div>
                {r.detail && <div className="report-entry-detail">檢舉人補充：{r.detail}</div>}
                <div className="snapshot" style={{ maxHeight: 120, marginTop: 6 }}>
                  {r.contextSnapshot || '（無內容）'}
                </div>
                {r.resolution && (
                  <div className="report-entry-detail">處理結果：{r.resolution}（{formatDate(r.reviewedAt)}）</div>
                )}
              </div>
            ))}
          </div>

          <div className="card">
            <h2 className="section-title">處置歷史</h2>
            {user.sanctions.length === 0 && <div className="page-sub">尚無紀錄</div>}
            {user.sanctions.map((s, i) => (
              <div className="field-row" key={i}>
                <span className="field-label">{SANCTION_LABEL[s.action] ?? s.action}</span>
                <span className="field-value">
                  <div className="mono">{formatDate(s.createdAt)}　by {s.actorId}</div>
                  {s.reason && <div>{s.reason}</div>}
                  {s.expiresAt && <div className="mono">至 {formatDate(s.expiresAt)}</div>}
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </>
  )
}
