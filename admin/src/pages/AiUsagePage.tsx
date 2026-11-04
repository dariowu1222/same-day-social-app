import { useEffect, useState } from 'react'
import {
  getAiUsage,
  getAiUsageLogs,
  type AiUsageBucket,
  type AiUsageSummary,
  type PagedAiUsage,
} from '../api'
import { Pagination, formatDate } from '../ui'

// 單筆成本在 US$0.00005 這個量級，用一般的兩位小數會整欄變成 $0.00。
//
// 彙總也一樣會中招：剛開始用的時候累計只有幾筆，四位小數照樣顯示 $0.0000，
// 畫面上看起來像沒花到錢、或像功能沒在跑。所以金額小到指定位數吃不下時
// 自動多給兩位，而不是把所有欄位都固定成六位（量大之後那樣很難讀）。
const usd = (v: number, digits = 4) => {
  const d = v > 0 && v < 10 ** -digits ? digits + 2 : digits
  return `US$${v.toFixed(d)}`
}

const tokens = (v: number) => v.toLocaleString('zh-TW')

// 單價一欄裡同時會出現 0.05 與 0.005，統一成固定位數會讓其中一個變難讀
// （0.050 / 0.005），所以分段：一分錢以下給三位，其餘兩位。
// 少了這個，0.4 會原樣印成「US$0.4」，跟旁邊的 US$0.05 排在一起很刺眼。
const price = (v: number) => `US$${v < 0.01 ? v.toFixed(3) : v.toFixed(2)}`

// 後端 CallStatus 的對照。鍵要和 AiTodayAnalyzer.CallStatus 一致。
const STATUS_LABEL: Record<string, string> = {
  OK: '成功',
  TRUNCATED: '被截斷',
  REFUSED: '模型拒絕',
  EMPTY_CONTENT: '沒有內容',
  PARSE_FAILED: '解析失敗',
  TIMEOUT: '逾時',
  ERROR: '錯誤',
}

function StatCard({ title, bucket }: { title: string; bucket: AiUsageBucket }) {
  return (
    <div className="card">
      <div className="stat-label">{title}</div>
      <div className="stat-value">{usd(bucket.costUsd)}</div>
      <span className="page-sub">
        {bucket.calls} 次呼叫
        {bucket.failedCalls > 0 && `（${bucket.failedCalls} 次失敗）`}
        ．輸入 {tokens(bucket.inputTokens)} ／ 輸出 {tokens(bucket.outputTokens)} token
      </span>
    </div>
  )
}

export default function AiUsagePage() {
  const [summary, setSummary] = useState<AiUsageSummary | null>(null)
  const [logs, setLogs] = useState<PagedAiUsage | null>(null)
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    void load()
  }, [status, page])

  async function load() {
    setLoading(true)
    setError('')
    try {
      // 兩支一起打：摘要與明細是同一個畫面的兩半，分開載會先閃一半出來
      const [s, l] = await Promise.all([getAiUsage(), getAiUsageLogs({ status, page })])
      setSummary(s)
      setLogs(l)
    } catch (e) {
      setError(e instanceof Error ? e.message : '載入失敗')
    } finally {
      setLoading(false)
    }
  }

  const totalPages = logs ? Math.max(1, Math.ceil(logs.total / logs.pageSize)) : 1

  return (
    <>
      <div className="page-head">
        <h1 className="page-title">AI 用量</h1>
        <span className="page-sub">「今日」標籤分析的 OpenAI 呼叫紀錄與估算花費</span>
      </div>

      {error && <div className="msg msg-error">{error}</div>}
      {!summary && !error && <div className="empty">載入中…</div>}

      {summary && (
        <>
          {!summary.storageReady && (
            // 這個專案不跑 EF migration，建表 SQL 要人工貼到 Supabase 執行。
            // 沒跑過的話下面整頁都會是 0，不講清楚會以為是「還沒人用」。
            <div className="msg msg-error">
              ai_usage_logs 資料表還不存在，用量沒有地方可以記。
              請先在 Supabase SQL Editor 執行
              backend/Infrastructure/Persistence/MigrationsSql/20261104_ai_usage_logs.sql。
            </div>
          )}

          {!summary.configured && (
            // 沒設 key 的時候整條分析是關的。不講清楚的話，看到一排 0
            // 會以為是「沒人用」，而不是「根本沒開」。
            <div className="msg msg-error">
              後端沒有設定 Ai:ApiKey，AI 分析目前整條停用中。下面的數字只是過去的紀錄，
              現在送出的今日一律走關鍵字分析器。
            </div>
          )}

          <div className="stat-grid">
            <StatCard title="今天" bucket={summary.today} />
            <StatCard title="近 7 天" bucket={summary.last7Days} />
            <StatCard title="本月" bucket={summary.thisMonth} />
            <StatCard title="累計" bucket={summary.allTime} />
          </div>

          <div className="stat-grid">
            <div className="card">
              <div className="stat-label">目前模型</div>
              <div className="stat-value" style={{ fontSize: '18px' }}>{summary.model}</div>
              <span className="page-sub">
                輸入 {price(summary.inputPricePerMTok)} ／ 快取 {price(summary.cachedPricePerMTok)} ／
                輸出 {price(summary.outputPricePerMTok)}（每百萬 token）
              </span>
            </div>
            <div className="card">
              <div className="stat-label">平均延遲</div>
              <div className="stat-value">{summary.averageLatencyMs}<span style={{ fontSize: '14px' }}> ms</span></div>
              <span className="page-sub">不含逾時與未送出的呼叫</span>
            </div>
            <div className="card">
              <div className="stat-label">每千則估算</div>
              <div className="stat-value">
                {summary.allTime.calls > 0
                  ? usd((summary.allTime.costUsd / summary.allTime.calls) * 1000, 3)
                  : '—'}
              </div>
              <span className="page-sub">依累計平均推估，樣本少時僅供參考</span>
            </div>
            <div className="card">
              <div className="stat-label">最後一次呼叫</div>
              <div className="stat-value" style={{ fontSize: '16px' }}>
                {formatDate(summary.lastCallAt)}
              </div>
            </div>
          </div>
        </>
      )}

      <div className="toolbar">
        <select
          className="input"
          value={status}
          onChange={(e) => { setStatus(e.target.value); setPage(1) }}
        >
          <option value="">全部狀態</option>
          <option value="FAILED">只看失敗</option>
          <option value="OK">成功</option>
          <option value="TRUNCATED">被截斷</option>
          <option value="REFUSED">模型拒絕</option>
          <option value="TIMEOUT">逾時</option>
          <option value="ERROR">錯誤</option>
        </select>
        <button className="btn" onClick={() => void load()} disabled={loading}>
          {loading ? '載入中…' : '重新整理'}
        </button>
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>時間</th>
              <th>狀態</th>
              <th>模型</th>
              <th>輸入</th>
              <th>輸出</th>
              <th>推理</th>
              <th>延遲</th>
              <th>估算費用</th>
            </tr>
          </thead>
          <tbody>
            {logs?.items.map((x) => (
              <tr key={x.id}>
                <td className="mono">{formatDate(x.createdAt)}</td>
                <td>
                  {STATUS_LABEL[x.status] ?? x.status}
                  {x.error && <div className="page-sub">{x.error}</div>}
                </td>
                <td className="mono">{x.model}</td>
                <td className="mono">
                  {tokens(x.inputTokens)}
                  {x.cachedInputTokens > 0 && ` (快取 ${tokens(x.cachedInputTokens)})`}
                </td>
                <td className="mono">{tokens(x.outputTokens)}</td>
                {/* 推理 token 算在輸出價裡，是這顆模型最容易暴衝的一項，所以單獨一欄 */}
                <td className="mono">{tokens(x.reasoningTokens)}</td>
                <td className="mono">{x.latencyMs} ms</td>
                <td className="mono">{usd(x.costUsd, 6)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {!loading && logs?.items.length === 0 && (
          <div className="empty">還沒有任何 AI 呼叫紀錄</div>
        )}
        {loading && <div className="empty">載入中…</div>}
      </div>

      {logs && logs.total > logs.pageSize && (
        <div className="pager">
          <Pagination page={logs.page} totalPages={totalPages} onChange={setPage} />
          <span className="pager-info">第 {logs.page} / {totalPages} 頁，共 {logs.total} 筆</span>
        </div>
      )}
    </>
  )
}
