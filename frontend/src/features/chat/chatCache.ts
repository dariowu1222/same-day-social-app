// 聊天資料的跨分頁快取。
//
// ChatPage 掛在 React Router 的 /chat 路由下，切到其他分頁時元件會被銷毀、
// 所有 state 歸零；切回來就得重抓房間、各房訊息、對方 profile、設定，
// 第一次進聊天室因此會空等一段時間。
//
// 這裡把資料留在模組層級（元件生命週期之外），切回分頁時可以先把上次的內容
// 直接放上畫面，再於背景刷新。快取以 userId 綁定，換人登入自動失效。
import type { ChatMessage, ChatRoom, ChatMemberSetting } from './types'
import type { UserProfile } from '../profile/types'

export type ChatCacheData = {
  rooms: ChatRoom[]
  roomMessages: Record<string, ChatMessage[]>
  profiles: Record<string, UserProfile>
  settings: Record<string, ChatMemberSetting>
}

const emptyData = (): ChatCacheData => ({ rooms: [], roomMessages: {}, profiles: {}, settings: {} })

let cachedUserId: string | null = null
let cachedData: ChatCacheData = emptyData()

/** 讀取此使用者的快取；沒有或不是同一人時回 null，呼叫端就走空白初始狀態。 */
export function readChatCache(userId: string | undefined): ChatCacheData | null {
  if (!userId || cachedUserId !== userId) return null
  return cachedData
}

/** 寫入快取。userId 換人時整份重來，避免看到上一個帳號的對話。 */
export function writeChatCache(userId: string | undefined, data: ChatCacheData): void {
  if (!userId) return
  if (cachedUserId !== userId) {
    cachedUserId = userId
    cachedData = emptyData()
  }
  cachedData = data
}

/** 登出等情境下主動清空。 */
export function clearChatCache(): void {
  cachedUserId = null
  cachedData = emptyData()
}
