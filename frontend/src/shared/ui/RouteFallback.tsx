/**
 * 分頁的程式碼還在路上時先畫這個（見 shared/lib/lazyPage.tsx）。
 * 前 0.4 秒什麼都不顯示，免得每次切頁都閃一下字——延遲寫在 CSS 裡。
 */
export default function RouteFallback() {
  return (
    <div className="page route-fallback" aria-busy="true">
      <p className="route-fallback-text">載入中</p>
    </div>
  )
}

/**
 * 分頁的程式碼抓不下來時顯示的畫面。
 *
 * 不丟例外：這個 App 沒有錯誤邊界，lazy 元件一丟例外整棵樹就卸載、畫面全白，
 * 連底部導覽都沒了。換成一頁說明加一顆按鈕，至少還切得到其他已經載好的分頁。
 */
export function RouteLoadFailed() {
  return (
    <div className="page route-fallback is-failed" role="alert">
      <p className="route-fallback-text">這一頁沒有載入成功</p>
      <p className="route-fallback-sub">可能是網路斷了，或 App 剛更新過</p>
      <button type="button" className="keycap-btn route-fallback-btn" onClick={() => window.location.reload()}>
        重新載入
      </button>
    </div>
  )
}
