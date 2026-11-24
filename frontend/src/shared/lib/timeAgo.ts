// 「18 分鐘前」這種相對時間。
//
// 樹洞卡片原本用的是 toLocaleDateString（「11/24 下午03:12」）。牆上的東西
// 「多久以前」比「幾點幾分」有用——人在意的是這句話是剛說的還是昨天說的。
// （通知中心有一份自己的 relativeTime，寫法幾乎一樣；這次沒有動它。）
export function timeAgo(iso: string | undefined | null): string {
  if (!iso) return ''
  const then = new Date(iso).getTime()
  if (Number.isNaN(then)) return ''
  const min = Math.floor((Date.now() - then) / 60000)
  if (min < 1) return '剛剛'
  if (min < 60) return `${min} 分鐘前`
  const hr = Math.floor(min / 60)
  if (hr < 24) return `${hr} 小時前`
  const day = Math.floor(hr / 24)
  if (day < 7) return `${day} 天前`
  return new Date(then).toLocaleDateString('zh-TW', { month: 'numeric', day: 'numeric' })
}
