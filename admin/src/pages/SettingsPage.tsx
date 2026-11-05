import { useEffect, useState } from 'react'
import {
  getAppSettings,
  runChatExpiry,
  updateAppSettings,
  type AppSettings,
  type ChatExpiryOutcome,
} from '../api'
import { formatDate } from '../ui'

/** 空字串、小數、負號都要擋在送出之前——後端會回 400，但在這裡擋掉才看得到是哪一格。 */
const asCount = (raw: string): number | null =>
  /^\d+$/.test(raw.trim()) ? Number(raw) : null

// 範圍用後端回來的上下限，不要在前端另外寫一份數字（AdminSettingsController 也是這樣交代的）：
// 自己抄一份的話，後端哪天放寬了，這個畫面會繼續擋著不給改。
function rangeError(raw: string, min: number, max: number, label: string): string {
  const value = asCount(raw)
  if (value === null) return `${label}要填正整數。`
  if (value < min || value > max) return `${label}必須介於 ${min} 到 ${max} 之間。`
  return ''
}

export default function SettingsPage() {
  const [settings, setSettings] = useState<AppSettings | null>(null)
  const [idleDays, setIdleDays] = useState('')
  const [maxPartners, setMaxPartners] = useState('')
  // 送出時才驗、但一改動就清掉：錯誤是「上一次送出的結果」，
  // 使用者照著訊息改完之後還看到紅字，會以為自己又填錯了別的東西。
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [outcome, setOutcome] = useState<ChatExpiryOutcome | null>(null)

  useEffect(() => { void load() }, [])

  async function load() {
    setError('')
    try {
      apply(await getAppSettings())
    } catch (e) {
      setError(e instanceof Error ? e.message : '載入失敗')
    }
  }

  // 輸入框一律跟著後端回來的值走，不要沿用剛剛送出去的數字：
  // 真正生效的是後端存下來的那個，畫面顯示的也該是它。
  function apply(next: AppSettings) {
    setSettings(next)
    setIdleDays(String(next.chatIdleDays))
    setMaxPartners(String(next.chatMaxPartners))
  }

  async function save() {
    if (!settings || busy) return

    const invalid =
      rangeError(idleDays, settings.chatIdleDaysMin, settings.chatIdleDaysMax, '閒置天數')
      || rangeError(maxPartners, settings.chatMaxPartnersMin, settings.chatMaxPartnersMax, '聊天對象上限')
    if (invalid) {
      setNotice('')
      setError(invalid)
      return
    }

    const days = Number(idleDays)
    const partners = Number(maxPartners)
    const changedDays = days !== settings.chatIdleDays
    const changedPartners = partners !== settings.chatMaxPartners
    if (!changedDays && !changedPartners) {
      setError('')
      setNotice('沒有任何變更。')
      return
    }

    setBusy(true)
    setError('')
    setNotice('')
    try {
      apply(await updateAppSettings({
        chatIdleDays: changedDays ? days : undefined,
        chatMaxPartners: changedPartners ? partners : undefined,
      }))
      setNotice('已儲存。新規則立刻生效，不必重新部署。')
    } catch (e) {
      // 後端 INVALID_VALUE 的訊息本來就是中文、而且會寫出合法範圍，原樣顯示比自己改寫清楚
      setError(e instanceof Error ? e.message : '儲存失敗')
    } finally {
      setBusy(false)
    }
  }

  async function runNow() {
    if (!settings || busy) return

    // 這顆按鈕會真的寄出通知、真的鎖掉房間，兩者都收不回來，所以不能一點就跑
    const ok = window.confirm(
      `現在就依「閒置 ${settings.chatIdleDays} 天」跑一次到期處理？\n\n`
      + '做的事跟每天早上九點那次排程完全一樣：達到門檻的聊天室會立刻收到「明天關閉」'
      + '的站內通知，已經通知滿一天又還是沒人回話的會當場鎖定。\n'
      + '通知寄出去收不回來，鎖定也沒有復原鍵。')
    if (!ok) return

    setBusy(true)
    setError('')
    setNotice('')
    setOutcome(null)
    try {
      setOutcome(await runChatExpiry())
    } catch (e) {
      setError(e instanceof Error ? e.message : '執行失敗')
    } finally {
      setBusy(false)
    }
  }

  if (!settings) {
    return error
      ? <div className="msg msg-error">{error}</div>
      : <div className="empty">載入中…</div>
  }

  // 影響預告只在輸入值本身合法時才算得準；填到一半的空格不該跳出「調短了」。
  // 連範圍也要看：0 是合法的整數但超出下限，照算的話會跳出「停在 0 天前的
  // 聊天室」這種沒有意義的句子，而且跟旁邊那行錯誤訊息自相矛盾。
  const inRange = (raw: string, min: number, max: number) => {
    const value = asCount(raw)
    return value === null || value < min || value > max ? null : value
  }
  const draftDays = inRange(idleDays, settings.chatIdleDaysMin, settings.chatIdleDaysMax)
  const draftPartners = inRange(maxPartners, settings.chatMaxPartnersMin, settings.chatMaxPartnersMax)

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">規則設定</h1>
          <span className="page-sub">聊天室的閒置期限與配對上限。存檔即生效，變更會記進審核軌跡。</span>
        </div>
      </div>

      {error && <div className="msg msg-error">{error}</div>}
      {notice && <div className="msg msg-ok">{notice}</div>}

      <div className="detail-grid">
        <div style={{ display: 'grid', gap: 16 }}>
          <div className="card">
            <h2 className="section-title">聊天室閒置鎖定</h2>
            <div className="page-sub" style={{ marginBottom: 12 }}>
              聊天室連續這麼多天沒有任何訊息就會被鎖定。鎖定不是刪除：房間還留在列表上、
              歷史看得到，但雙方只剩「退出聊天室」可以按。
              <br />
              鎖定前一天雙方會先收到一則站內通知，所以最後還有一天可以把話接回去。
            </div>

            <label>
              <div className="stat-label">
                閒置天數（{settings.chatIdleDaysMin}–{settings.chatIdleDaysMax}）
              </div>
              <input
                className="input"
                type="number"
                min={settings.chatIdleDaysMin}
                max={settings.chatIdleDaysMax}
                value={idleDays}
                onChange={(e) => { setIdleDays(e.target.value); setError('') }}
              />
            </label>

            {draftDays !== null && draftDays < settings.chatIdleDays && (
              <div className="msg msg-warn" style={{ margin: '12px 0 0' }}>
                調短：{settings.chatIdleDays} → {draftDays} 天。
                最後一則訊息停在 {draftDays} 天前的聊天室，會在下一次排程收到
                「明天關閉」的通知，再隔一天才真的鎖定。存檔後就算數，通知寄出去收不回來。
              </div>
            )}
            {draftDays !== null && draftDays > settings.chatIdleDays && (
              <div className="msg msg-warn" style={{ margin: '12px 0 0' }}>
                調長：{settings.chatIdleDays} → {draftDays} 天。
                之後的聊天室會晚一點才到期，但已經被鎖定的房間不會因此解鎖。
              </div>
            )}
          </div>

          <div className="card">
            <h2 className="section-title">同時聊天對象上限</h2>
            <div className="page-sub" style={{ marginBottom: 12 }}>
              每個人同時進行中的聊天對象上限。額滿的人配不到新的對象，別人也配不到他。
              <br />
              已經鎖定的房間不算在內，所以對話自然沉寂、房間到期之後，名額會自己回來。
            </div>

            <label>
              <div className="stat-label">
                人數上限（{settings.chatMaxPartnersMin}–{settings.chatMaxPartnersMax}）
              </div>
              <input
                className="input"
                type="number"
                min={settings.chatMaxPartnersMin}
                max={settings.chatMaxPartnersMax}
                value={maxPartners}
                onChange={(e) => { setMaxPartners(e.target.value); setError('') }}
              />
            </label>

            {draftPartners !== null && draftPartners < settings.chatMaxPartners && (
              <div className="msg msg-warn" style={{ margin: '12px 0 0' }}>
                調低：{settings.chatMaxPartners} → {draftPartners} 人。
                目前手上已經有 {draftPartners} 個（含）以上對象的人，
                在舊房間到期之前完全配不到新的人——他們在滑卡頁會一直配不出結果。
              </div>
            )}
          </div>

          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-primary" onClick={() => void save()} disabled={busy}>
              {busy ? '處理中…' : '儲存變更'}
            </button>
            <button className="btn" onClick={() => apply(settings)} disabled={busy}>
              還原
            </button>
          </div>
        </div>

        <div style={{ display: 'grid', gap: 16 }}>
          <div className="card">
            <h2 className="section-title">目前生效</h2>
            <div className="field-row">
              <span className="field-label">閒置天數</span>
              <span className="field-value">{settings.chatIdleDays} 天</span>
            </div>
            <div className="field-row">
              <span className="field-label">對象上限</span>
              <span className="field-value">{settings.chatMaxPartners} 人</span>
            </div>
            <div className="field-row">
              <span className="field-label">寬限起點</span>
              <span className="field-value">
                {settings.chatExpirySince ? formatDate(settings.chatExpirySince) : '未設定'}
                <div className="page-sub">
                  功能上線前就存在的聊天室，到期時間取「最後一則訊息」與這個時間的較晚者，
                  不會一上線就被回溯鎖定。此值只能從資料庫調整。
                </div>
              </span>
            </div>
          </div>

          <div className="card">
            <h2 className="section-title">立即執行一次到期處理</h2>
            <div className="page-sub" style={{ marginBottom: 12 }}>
              排程每天台灣時間早上九點跑一次。剛調完天數想馬上知道影響多少間時可以手動跑一次，
              不必等到隔天早上。
              <br />
              用的是<strong>已儲存</strong>的值——左邊改了還沒存的數字不算數。
            </div>
            <button className="btn btn-danger" onClick={() => void runNow()} disabled={busy}>
              {busy ? '執行中…' : '執行一次（會寄通知、會鎖房間）'}
            </button>

            {outcome && !outcome.storageReady && (
              // 這個專案不跑 EF migration，建表 SQL 要人工貼到 Supabase 執行。
              // 沒跑過的話三個數字都會是 0，不講清楚會以為是「剛好沒有到期的房間」。
              <div className="msg msg-error" style={{ margin: '12px 0 0' }}>
                聊天到期用的欄位還不存在，這次什麼都沒做。
                請先在 Supabase SQL Editor 執行
                backend/Infrastructure/Persistence/MigrationsSql/20261104_chat_expiry_and_app_settings.sql。
              </div>
            )}

            {outcome && outcome.storageReady && (
              <div style={{ marginTop: 12 }}>
                <div className="field-row">
                  <span className="field-label">掃描</span>
                  <span className="field-value">{outcome.scanned} 間（尚未鎖定的房間）</span>
                </div>
                <div className="field-row">
                  <span className="field-label">發出通知</span>
                  <span className="field-value">{outcome.notified} 間</span>
                </div>
                <div className="field-row">
                  <span className="field-label">鎖定</span>
                  <span className="field-value">{outcome.expired} 間</span>
                </div>
                <div className="page-sub" style={{ marginTop: 8 }}>
                  通知與鎖定是分兩天的：這次剛通知的房間，要等下一次執行才會被鎖。
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </>
  )
}
