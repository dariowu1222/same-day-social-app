// AI 智慧篩選開關。
//
// 放在今日頁標題旁邊，是給測試期用的：開著就讓後端再用一次 OpenAI 分析
// 「今日」的標籤，關掉就走原本的關鍵字分析器。兩條路徑都保留，隨時可以
// 比對差異。
//
// 為什麼存在 localStorage 而不是 user_settings：這張表要加欄位得走
// Supabase SQL Editor（app 用的角色沒有 DDL 權限），而這是測試期的開關、
// 不是帳號設定。要轉正的時候再搬進 user_settings。
//
// 預設關閉：後端沒設 Ai:ApiKey 時這個開關本來就不會有作用，預設開著只會
// 讓人以為有在用。

const KEY = 'same-day-ai-filter'

export function isAiFilterOn(): boolean {
  try {
    return localStorage.getItem(KEY) === '1'
  } catch {
    // 私密視窗或關掉站台資料時會丟例外，當成關閉
    return false
  }
}

export function setAiFilter(on: boolean) {
  try {
    if (on) localStorage.setItem(KEY, '1')
    else localStorage.removeItem(KEY)
  } catch {
    // 存不起來就算了，這一輪仍然會依畫面上的狀態送出
  }
}
