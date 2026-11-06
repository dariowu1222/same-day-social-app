// 樹洞留言框：情境提示 + 快捷回覆（詳情頁與列表頁共用）

// 情境留言提示（一次性 nudge）。只是想說＝中性，不顯示提示。
export const COMPOSE_HINTS: Record<string, string> = {
  COMFORT_ME: '他現在想被接住，一句溫柔的話就夠了',
  GIVE_ADVICE: '他想聽聽你的想法，溫和地給點建議吧',
  RANT_TOGETHER: '他想找人站在同一邊，先別急著給建議',
  DISTRACT_ME: '他想暫時放鬆，聊點別的、開個玩笑都好',
  FIND_SIMILAR: '他想知道自己不孤單，說說你的類似經歷',
}

// 情境快捷回覆（常駐橫條）
//
// JUST_SAYING 沒有快捷鍵，是故意的。它原本有「我看到了」「謝謝你願意說」兩顆，
// 但那等於 App 自己掛了一塊牌子說「這樣回就可以了」——一鍵就送得出一句完整的話，
// 結果整個版面都是「我看到了」。那兩句本身沒問題，有問題的是我們在背書。
// 現在它們搬去當輸入框的預設提示：看得到、照著寫得出來，但要自己打。
//
// 其他情境留著，因為它們多半是句子的開頭（「我的經驗是…」）或情緒的碎片
// （「抱抱」「站你這邊」），本來就預期你會接著寫下去，不是一句話交差。
export const QUICK_REPLIES: Record<string, string[]> = {
  COMFORT_ME: ['抱抱', '我懂這種感覺', '你已經很努力了', '辛苦了，先好好休息', '我在這裡陪你'],
  GIVE_ADVICE: ['我的經驗是…', '也許可以試試…', '我會這樣做'],
  RANT_TOGETHER: ['真的很扯', '我也遇過', '站你這邊', '太誇張了吧'],
  DISTRACT_ME: ['來說點別的～', '推你一部劇', '看看這個'],
  FIND_SIMILAR: ['我也是這樣', '+1 同款', '找到組織了'],
}

// 提示一次性記憶：以 postId 持久化於 localStorage（方案 A，永不再顯示）
const HINT_SEEN_KEY = 'same-day-rant-hint-seen'

export function getHintSeenSet(): Set<string> {
  try { return new Set(JSON.parse(localStorage.getItem(HINT_SEEN_KEY) || '[]')) } catch { return new Set() }
}

export function markHintSeen(id: string) {
  const s = getHintSeenSet()
  s.add(id)
  localStorage.setItem(HINT_SEEN_KEY, JSON.stringify([...s]))
}

// 快捷回覆帶入：已有文字時以「，」串接（結尾已是標點/空白則不加）
export function appendQuickReply(prev: string, text: string): string {
  if (!prev) return text
  return /[\s，。！？、,.!?]$/.test(prev) ? prev + text : prev + '，' + text
}

// ── 回覆輸入框的預設提示 ──
//
// 每次打開留言框隨機抽一句，打開之後就不動。
// 為什麼不是輪播：樹洞是人難受的時候來的地方，一個人正在猶豫要不要回話，
// 而框框每幾秒自己換字，那是一種很低但真實的催促感。抽籤一樣看得到變化
// （滑過一整頁會遇到不同的句子），但沒有任何東西在你眼前動。
//
// 這是 placeholder 屬性，不是 ghost text——按 Tab 只會跳走，帶不進任何字。
// 要能 Tab 補字得另外寫鍵盤處理，所以「不讓他用 Tab」靠的是不做，不是擋。
// 除了 JUST_SAYING 之外，這裡的句子都刻意不跟同情境的 QUICK_REPLIES 重複。
// 重複的話，提示文字會跟正上方那排按鈕長一樣，看起來像在叫你按那顆鈕——
// 又繞回「App 幫你把話講完」的老問題。
export const REPLY_PLACEHOLDERS: Record<string, string[]> = {
  JUST_SAYING: ['我看到了', '謝謝你願意說', '你說出來了，這很不容易', '我讀完了，在這裡', '有人聽見了'],
  COMFORT_ME: ['這樣很難受吧', '不用急著好起來', '你撐到現在了', '慢慢來，我在', '想到你，先來看看你'],
  GIVE_ADVICE: ['想先問你一件事', '有個角度給你參考', '說說我看到的部分', '不一定對，但我想說', '換個方向想想看'],
  RANT_TOGETHER: ['這換誰都會氣', '聽完我也上火', '一起罵', '他憑什麼啊', '這真的不是你的問題'],
  DISTRACT_ME: ['跟你說個無關的事', '要不要聽個笑話', '換個話題好了', '我剛看到一件蠢事', '走，聊點輕鬆的'],
  FIND_SIMILAR: ['原來不只我一個', '這段我也走過', '當年我也是', '我懂那個階段', '說說我的版本'],
}

// 情境對不上時的保底（舊資料或之後新增的 mode）
export const DEFAULT_REPLY_PLACEHOLDER = '留一句溫和回應'

// exclude 傳上一次抽到的那句，避免連開兩次都一樣——真的試過，五句的池子
// 連三次同一句並不罕見，而連續看到同一句的感覺就跟寫死沒兩樣。
export function pickReplyPlaceholder(mode: string, exclude?: string): string {
  const pool = REPLY_PLACEHOLDERS[mode]
  if (!pool?.length) return DEFAULT_REPLY_PLACEHOLDER
  const rest = pool.filter((p) => p !== exclude)
  const from = rest.length > 0 ? rest : pool
  return from[Math.floor(Math.random() * from.length)]
}
