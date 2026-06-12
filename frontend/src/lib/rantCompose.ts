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
export const QUICK_REPLIES: Record<string, string[]> = {
  JUST_SAYING: ['我看到了', '謝謝你願意說'],
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
