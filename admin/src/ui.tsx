// 後台共用的顯示小元件與格式化。

import type { AccountStatus } from './api'

const STATUS_LABEL: Record<AccountStatus, string> = {
  ACTIVE: '正常',
  SUSPENDED: '停權中',
  BANNED: '已封鎖',
  DELETED: '已註銷',
}

const STATUS_CLASS: Record<AccountStatus, string> = {
  ACTIVE: 'badge-active',
  SUSPENDED: 'badge-suspended',
  BANNED: 'badge-banned',
  DELETED: 'badge-deleted',
}

export function StatusBadge({ status }: { status: AccountStatus }) {
  return <span className={`badge ${STATUS_CLASS[status] ?? 'badge-deleted'}`}>{STATUS_LABEL[status] ?? status}</span>
}

// 檢舉分類的中文對照，需與後端 ReportCategory 一致
export const CATEGORY_LABEL: Record<string, string> = {
  HARASSMENT: '騷擾或霸凌',
  SEXUAL: '色情或裸露',
  VIOLENCE: '暴力或威脅',
  SCAM: '詐騙或廣告',
  IMPERSONATION: '假冒他人',
  HATE: '仇恨言論',
  SELF_HARM: '自我傷害',
  OTHER: '其他',
}

export const TARGET_LABEL: Record<string, string> = {
  RANT_POST: '樹洞貼文',
  RANT_REPLY: '樹洞回應',
  CHAT_MESSAGE: '聊天訊息',
  USER: '使用者',
}

export const REPORT_STATUS_LABEL: Record<string, string> = {
  PENDING: '待處理',
  REVIEWING: '處理中',
  RESOLVED: '已處理',
  DISMISSED: '已駁回',
}

export const SANCTION_LABEL: Record<string, string> = {
  WARN: '警告',
  SUSPEND: '停權',
  UNSUSPEND: '解除停權',
  BAN: '封鎖',
  UNBAN: '解除封鎖',
  DELETE: '註銷',
  EDIT_PROFILE: '修改資料',
}

/** 嚴重度分級：80 以上（自我傷害、暴力）要一眼看得出來。 */
export function SeverityBadge({ severity }: { severity: number }) {
  const cls = severity >= 80 ? 'badge-sev-high' : severity >= 60 ? 'badge-sev-mid' : 'badge-sev-low'
  const label = severity >= 80 ? '高' : severity >= 60 ? '中' : '低'
  return <span className={`badge ${cls}`}>{label} {severity}</span>
}

export function formatDate(iso?: string | null): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleString('zh-TW', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit',
  })
}

/**
 * 分頁列：最前／上一頁／頁碼／下一頁／最末。
 * 頁數多時只顯示目前頁附近與頭尾，中間用省略號收合，
 * 避免上百頁時分頁列自己撐爆版面。
 */
export function Pagination({ page, totalPages, onChange }: {
  page: number
  totalPages: number
  onChange: (page: number) => void
}) {
  if (totalPages <= 1) return null

  const first = page <= 1
  const last = page >= totalPages

  return (
    <div className="pager">
      <button className="pager-btn" disabled={first} onClick={() => onChange(1)} title="第一頁">«</button>
      <button className="pager-btn" disabled={first} onClick={() => onChange(page - 1)} title="上一頁">‹</button>

      {pageItems(page, totalPages).map((item, i) =>
        item === GAP ? (
          <span className="pager-gap" key={`gap-${i}`}>…</span>
        ) : (
          <button
            key={item}
            className={`pager-btn${item === page ? ' active' : ''}`}
            onClick={() => onChange(item)}
          >
            {item}
          </button>
        ),
      )}

      <button className="pager-btn" disabled={last} onClick={() => onChange(page + 1)} title="下一頁">›</button>
      <button className="pager-btn" disabled={last} onClick={() => onChange(totalPages)} title="最後一頁">»</button>
    </div>
  )
}

const GAP = -1

/** 產生頁碼序列；GAP 代表省略號。 */
function pageItems(current: number, total: number): number[] {
  // 頁數少就全部列出，不必收合
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)

  const SIBLINGS = 1
  const left = Math.max(2, current - SIBLINGS)
  const right = Math.min(total - 1, current + SIBLINGS)
  const items: number[] = [1]

  if (left > 2) items.push(GAP)
  for (let i = left; i <= right; i++) items.push(i)
  if (right < total - 1) items.push(GAP)
  items.push(total)

  return items
}
