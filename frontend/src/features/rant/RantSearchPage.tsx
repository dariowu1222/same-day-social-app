import { useState, useEffect, useRef, useMemo, type ReactNode } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { ArrowLeft, X, Search, Clock } from 'lucide-react'
import { getRants } from './api'
import type { RantPost } from './types'
import { RANT_MODES, MODE_LABELS } from './rantModes'

const RECENT_KEY = 'same-day-rant-recent-searches'
const MAX_RECENT = 10

function loadRecent(): string[] {
  try {
    const v = JSON.parse(localStorage.getItem(RECENT_KEY) ?? '[]')
    return Array.isArray(v) ? v.filter((x) => typeof x === 'string') : []
  } catch { return [] }
}
function persistRecent(list: string[]) {
  localStorage.setItem(RECENT_KEY, JSON.stringify(list.slice(0, MAX_RECENT)))
}

// 取貼文摘要：若有關鍵字則以命中處為中心擷取，否則取開頭。
function snippetAround(content: string, q: string): string {
  const max = 80
  if (q) {
    const idx = content.toLowerCase().indexOf(q.toLowerCase())
    if (idx >= 0) {
      const start = Math.max(0, idx - 24)
      const slice = content.slice(start, start + max)
      return (start > 0 ? '…' : '') + slice + (start + max < content.length ? '…' : '')
    }
  }
  return content.length > max ? content.slice(0, max) + '…' : content
}

// 內文命中關鍵字高亮。
function highlight(text: string, q: string): ReactNode {
  if (!q) return text
  const lower = text.toLowerCase()
  const ql = q.toLowerCase()
  const parts: ReactNode[] = []
  let i = 0, key = 0
  while (i <= text.length) {
    const found = lower.indexOf(ql, i)
    if (found < 0) { parts.push(text.slice(i)); break }
    if (found > i) parts.push(text.slice(i, found))
    parts.push(<mark key={key++} className="rant-search-hl">{text.slice(found, found + q.length)}</mark>)
    i = found + q.length
  }
  return parts
}

export default function RantSearchPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const initialQ = (location.state as { q?: string } | null)?.q ?? ''
  const [query, setQuery] = useState(initialQ)
  const [modeFilter, setModeFilter] = useState<string | null>(null)
  const [posts, setPosts] = useState<RantPost[]>([])
  const [recent, setRecent] = useState<string[]>(loadRecent)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => { getRants(undefined, 50).then((res) => setPosts(res.data)).catch(() => {}) }, [])
  useEffect(() => { inputRef.current?.focus() }, [])

  const isResultState = query.trim() !== '' || modeFilter !== null

  const results = useMemo(() => {
    if (modeFilter) return posts.filter((p) => p.mode === modeFilter)
    const q = query.trim().toLowerCase()
    if (!q) return []
    return posts.filter((p) =>
      p.content.toLowerCase().includes(q) ||
      (MODE_LABELS[p.mode] ?? '').toLowerCase().includes(q) ||
      p.hashtags?.some((t) => t.toLowerCase().includes(q)),
    )
  }, [posts, query, modeFilter])

  function recordRecent(term: string) {
    const t = term.trim()
    if (!t) return
    setRecent((prev) => {
      const next = [t, ...prev.filter((x) => x !== t)].slice(0, MAX_RECENT)
      persistRecent(next)
      return next
    })
  }

  function onType(v: string) { setQuery(v); setModeFilter(null) }
  function clearInput() { setQuery(''); setModeFilter(null); inputRef.current?.focus() }
  function openPost(p: RantPost) { if (query.trim()) recordRecent(query); navigate(`/rant/${p.id}`) }

  return (
    <div className="page rant-search-page">
      {/* 13.2 頂部列 */}
      <div className="rant-search-topbar">
        <button className="rant-search-back" type="button" onClick={() => navigate('/rant')} aria-label="返回樹洞">
          <ArrowLeft size={22} />
        </button>
        <div className="rant-search-field">
          <Search size={15} className="rant-search-field-icon" />
          <input
            ref={inputRef}
            className="rant-search-field-input"
            placeholder="搜尋樹洞的心事"
            value={query}
            onChange={(e) => onType(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter' && query.trim()) recordRecent(query) }}
          />
          {(query || modeFilter) && (
            <button className="rant-search-field-clear" type="button" onClick={clearInput} aria-label="清空">
              <X size={15} />
            </button>
          )}
        </div>
      </div>

      {!isResultState ? (
        // 13.3 預設態
        <div className="rant-search-default">
          <p className="rant-search-section-title">依心情瀏覽</p>
          <div className="rant-mood-chips">
            {RANT_MODES.map(({ key, label, Icon }) => (
              <button key={key} className="rant-mood-chip" type="button" onClick={() => { setModeFilter(key); setQuery('') }}>
                <Icon size={15} strokeWidth={1.9} /><span>{label}</span>
              </button>
            ))}
          </div>

          {recent.length > 0 && (
            <>
              <div className="rant-search-section-head">
                <p className="rant-search-section-title">最近搜尋</p>
                <button className="rant-recent-clear" type="button" onClick={() => { setRecent([]); persistRecent([]) }}>清除</button>
              </div>
              <div className="rant-recent-list">
                {recent.map((term) => (
                  <div key={term} className="rant-recent-item">
                    <button className="rant-recent-main" type="button" onClick={() => { setQuery(term); setModeFilter(null) }}>
                      <Clock size={15} className="rant-recent-icon" /><span>{term}</span>
                    </button>
                    <button
                      className="rant-recent-remove"
                      type="button"
                      aria-label="刪除這筆"
                      onClick={() => setRecent((prev) => { const next = prev.filter((x) => x !== term); persistRecent(next); return next })}
                    >
                      <X size={13} />
                    </button>
                  </div>
                ))}
              </div>
            </>
          )}
        </div>
      ) : (
        // 13.4 結果態
        <div className="rant-search-results">
          {results.length === 0 ? (
            <div className="rant-search-empty">
              <p>找不到相關的心事</p>
              <span>換個關鍵字試試</span>
            </div>
          ) : (
            results.map((p) => (
              <button key={p.id} className="rant-result-card" type="button" onClick={() => openPost(p)}>
                <span className="rant-result-avatar">{p.nickname?.[0] ?? '·'}</span>
                <span className="rant-result-body">
                  <span className="rant-result-head">
                    <span className="rant-result-name">{p.nickname}</span>
                    <span className="tag" data-mode={p.mode}>{MODE_LABELS[p.mode] ?? p.mode}</span>
                  </span>
                  <span className="rant-result-snippet">{highlight(snippetAround(p.content, query.trim()), query.trim())}</span>
                </span>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}
