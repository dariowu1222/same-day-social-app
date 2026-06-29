// 聊天安全：敏感詞偵測（只提示、不限制）與約見意圖偵測。
// 辱罵詞庫來自共用單一來源 shared/lexicon/safety-lexicon.json（改詞請改該檔並重新生成）。
import { ABUSE_ZH, ABUSE_EN } from './lexicon.generated'

export type RiskCategory = '聯絡方式' | '金錢' | '地址' | '個資'

const PATTERNS: { category: RiskCategory; re: RegExp }[] = [
  { category: '聯絡方式', re: /09\d{8}|\d{2,4}-\d{6,8}|https?:\/\/|www\.|line\b|賴|加賴|ig\b|instagram|微信|wechat|telegram|skype|whatsapp|手機號|電話號/i },
  { category: '金錢', re: /匯款|轉帳|轉帳|借錢|借款|付款|匯錢|訂金|押金|保證金|代購|刷卡|匯給|帳號|銀行帳|投資|穩賺|加密貨幣|虛擬貨幣/i },
  { category: '地址', re: /地址|住址|住哪|門牌|幾號幾樓|哪一區住/i },
  { category: '個資', re: /身分證|身份證|證件號|[A-Z][12]\d{8}/i },
]

// 回傳命中的風險類別（去重）。空陣列 = 無風險字眼。
export function detectRisks(text: string): RiskCategory[] {
  const found = new Set<RiskCategory>()
  for (const { category, re } of PATTERNS) {
    if (re.test(text)) found.add(category)
  }
  return [...found]
}

// 約見面意圖：用來在安排見面時自動跳一次安全提醒。
export function detectMeetupIntent(text: string): boolean {
  return /見面|碰面|見個面|出來見|約見|見一面|見一下|碰個面|約出來|要不要約|約.{0,4}出來|面交|當面/.test(text)
}

// ── 人身攻擊 / 辱罵偵測（送出前措辭提醒，只提示、不阻擋）──
// 詞庫只收完整詞、剔除易誤判單字（幹/屌/賤/笨/蠢/神經/有病/滾），來自共用 lexicon。

// 轉小寫 + 全形轉半形（比對前正規化）。
function toHalfWidthLower(text: string): string {
  return text
    .toLowerCase()
    .replace(/　/g, ' ')
    .replace(/[！-～]/g, (c) => String.fromCharCode(c.charCodeAt(0) - 0xFEE0))
}

// 偵測人身攻擊字眼：中文去空白標點後子字串比對；英文縮寫用詞界比對。
export function detectAbuse(text: string): boolean {
  const lower = toHalfWidthLower(text)
  const stripped = lower.replace(/[\s\p{P}]/gu, '')
  if (ABUSE_ZH.some((w) => stripped.includes(w))) return true
  return new RegExp(`\\b(${ABUSE_EN.join('|')})\\b`).test(lower)
}

export const CHAT_SAFETY_TIPS = [
  '不要透露金融資訊，也不要為了任何理由匯款或代購。',
  '聯絡方式、地址等個資，等你真的信任對方再分享。',
  '對方若急著要你離開平台或私下聯絡，請提高警覺。',
]

export const MEETUP_SAFETY_TIPS = [
  '第一次見面選白天、人多的公開場所。',
  '把見面的時間地點先告訴一位信任的朋友。',
  '自行前往與返家，全程保管好自己的飲料與物品。',
  '覺得不對勁就隨時離開，安全永遠優先。',
]
