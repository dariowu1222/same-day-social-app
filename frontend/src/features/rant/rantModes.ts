// 樹洞情境（mode）單一來源：key / 文案 / icon。
// 全 app 共用：發文情境選擇、貼文情境標籤、搜尋頁「依心情瀏覽」、留言情境提示。
// 改情境或 icon 只改這裡，其餘頁面自動同步。
import { MessageCircleMore, Heart, Lightbulb, Cloud, Sparkles, Users, type LucideIcon } from 'lucide-react'

export type RantMode = {
  key: string
  label: string
  Icon: LucideIcon
}

export const RANT_MODES: RantMode[] = [
  { key: 'JUST_SAYING', label: '只是想說', Icon: MessageCircleMore },
  { key: 'COMFORT_ME', label: '想被安慰', Icon: Heart },
  { key: 'GIVE_ADVICE', label: '想聽建議', Icon: Lightbulb },
  { key: 'RANT_TOGETHER', label: '想一起抱怨', Icon: Cloud },
  { key: 'DISTRACT_ME', label: '想轉移注意力', Icon: Sparkles },
  { key: 'FIND_SIMILAR', label: '想找同類', Icon: Users },
]

export const DEFAULT_RANT_MODE = 'JUST_SAYING'

// key → 文案（取代各頁各自抄一份的 MODE_LABELS）
export const MODE_LABELS: Record<string, string> = Object.fromEntries(
  RANT_MODES.map((m) => [m.key, m.label]),
)

// key → icon 元件
export const MODE_ICONS: Record<string, LucideIcon> = Object.fromEntries(
  RANT_MODES.map((m) => [m.key, m.Icon]),
)
