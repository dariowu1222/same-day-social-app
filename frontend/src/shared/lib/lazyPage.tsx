import { lazy, type ComponentType } from 'react'
import { RouteLoadFailed } from '../ui/RouteFallback'

const RELOADED_KEY = 'same-day-chunk-reloaded'

/**
 * 路由層級的延後載入。
 *
 * 以前 App.tsx 把 17 支頁面全部靜態 import，登入畫面出現之前就要先下載、解析
 * 整個 App 的程式碼。現在每一頁各自一包，用到才抓（App.tsx 會在第一個畫面畫完之後
 * 趁空檔把其餘的先抓好，所以切分頁不會等）。
 *
 * 抓不到的時候依序：
 *  1. 隔半秒再試一次——行動網路瞬斷最常見
 *  2. 還是不行、而且現在有網路：整頁重新載入一次。網頁版部署新版之後，舊的
 *     index 指向的那幾包檔名已經不存在，重試多少次都是 404，要重抓 index 才會好。
 *     只做一次（記在 sessionStorage），不然真的壞掉時會無限重新整理
 *  3. 都不行：顯示 RouteLoadFailed（shared/ui/RouteFallback.tsx）
 *
 * load 另外回傳出去，給 App.tsx 預抓用——同一個函式，瀏覽器只會抓一次。
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- 每一頁的 props 都不一樣，這裡只是轉手
export function lazyPage<T extends ComponentType<any>>(load: () => Promise<{ default: T }>) {
  const attempt = (): Promise<{ default: T }> =>
    load()
      .catch(() => new Promise<void>((resolve) => window.setTimeout(resolve, 500)).then(load))
      .then((loaded) => {
        try { sessionStorage.removeItem(RELOADED_KEY) } catch { /* 無痕模式之類，不影響 */ }
        return loaded
      })
      .catch(() => {
        let reloaded = true
        try {
          reloaded = sessionStorage.getItem(RELOADED_KEY) === '1'
          if (!reloaded) sessionStorage.setItem(RELOADED_KEY, '1')
        } catch { /* 讀寫不了就當作已經重載過，直接顯示失敗畫面 */ }

        if (!reloaded && navigator.onLine) {
          window.location.reload()
          // 重新載入中，不要先閃一下失敗畫面
          return new Promise<{ default: T }>(() => {})
        }
        return { default: RouteLoadFailed as unknown as T }
      })

  return { Page: lazy(attempt), preload: () => { void load().catch(() => {}) } }
}
