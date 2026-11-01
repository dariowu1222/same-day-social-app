/**
 * 滑卡表態送不出去時的本機暫存。
 *
 * 左右滑都是先推進畫面、再在背景送出，滑卡手感才不會被網路拖住。
 * 但兩種失敗要用完全不同的方式補救：
 *
 * - **左滑**（不想再看到）：記在本機擋掉就好。伺服器沒收到頂多是這個人
 *   之後還在候選池裡，對使用者沒有損失。
 * - **右滑**（想認識）：絕對不能只記在本機。伺服器沒收到就等於這次同頻
 *   從未發生，對方永遠配不到你；而本機又把人擋住的話，牌堆會假性見底、
 *   空狀態還顯示「都看過了」。所以右滑失敗要排進重送佇列，下次進頁面補送。
 *   後端的 Like 已經做成冪等（已右滑過就不再發通知），重送是安全的。
 *
 * 兩份都存時間戳，會自己過期，清單不會無限長大。
 */

const PASSED_KEY = 'match-passed-fallback'
const PENDING_LIKE_KEY = 'match-pending-likes'

/** 左滑的本機備援保留期，與後端的冷卻期一致。 */
const PASSED_TTL_MS = 30 * 24 * 60 * 60 * 1000

/** 右滑重送佇列的保留期。超過這麼久還送不出去，那張卡多半已經不在了。 */
const PENDING_TTL_MS = 7 * 24 * 60 * 60 * 1000

type Store = Record<string, number>

function read(key: string): Store {
  // 私密視窗、關掉站台資料的瀏覽器會在存取時直接丟例外，不能讓它炸掉滑卡頁
  try {
    const raw = localStorage.getItem(key)
    const parsed: unknown = raw ? JSON.parse(raw) : null
    return parsed && typeof parsed === 'object' ? (parsed as Store) : {}
  } catch {
    return {}
  }
}

function write(key: string, store: Store) {
  try {
    localStorage.setItem(key, JSON.stringify(store))
  } catch {
    // 寫不進去就算了，下次重新載入頂多再看到同一張卡
  }
}

function readFresh(key: string, ttl: number): Store {
  const store = read(key)
  const cutoff = Date.now() - ttl
  const kept: Store = {}
  let dropped = 0

  for (const [id, at] of Object.entries(store)) {
    if (typeof at === 'number' && at > cutoff) kept[id] = at
    else dropped += 1
  }

  if (dropped > 0) write(key, kept)
  return kept
}

/** 記下一筆送失敗的左滑。 */
export function rememberPassed(matchId: string) {
  const store = read(PASSED_KEY)
  store[matchId] = Date.now()
  write(PASSED_KEY, store)
}

/** 讀出還在保留期內、要從牌堆濾掉的 matchId。 */
export function readPassedIds(): Set<string> {
  return new Set(Object.keys(readFresh(PASSED_KEY, PASSED_TTL_MS)))
}

/** 記下一筆送失敗的右滑，等下次進頁面補送。 */
export function rememberPendingLike(matchId: string) {
  const store = read(PENDING_LIKE_KEY)
  store[matchId] = Date.now()
  write(PENDING_LIKE_KEY, store)
}

/** 還沒補送成功的右滑。 */
export function readPendingLikes(): string[] {
  return Object.keys(readFresh(PENDING_LIKE_KEY, PENDING_TTL_MS))
}

/** 補送成功，從佇列移除。 */
export function clearPendingLike(matchId: string) {
  const store = read(PENDING_LIKE_KEY)
  if (!(matchId in store)) return
  delete store[matchId]
  write(PENDING_LIKE_KEY, store)
}
