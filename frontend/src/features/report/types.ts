// 檢舉標的類型，需與後端 ReportTargetType 一致
export type ReportTargetType = 'RANT_POST' | 'RANT_REPLY' | 'CHAT_MESSAGE' | 'USER'

// 檢舉分類，需與後端 ReportCategory 一致
export type ReportCategory =
  | 'HARASSMENT'
  | 'SEXUAL'
  | 'VIOLENCE'
  | 'SCAM'
  | 'IMPERSONATION'
  | 'HATE'
  | 'SELF_HARM'
  | 'OTHER'

export type ReportOption = {
  value: ReportCategory
  label: string
  hint: string
}

// SELF_HARM 刻意不用「檢舉」的語氣：那不是舉發違規，是替朋友求救。
// 措辭若寫成指控，會讓想幫忙的人猶豫要不要按下去。
export const REPORT_OPTIONS: ReportOption[] = [
  { value: 'HARASSMENT', label: '騷擾或霸凌', hint: '人身攻擊、持續騷擾' },
  { value: 'SEXUAL', label: '色情或裸露', hint: '性暗示、不當影像' },
  { value: 'VIOLENCE', label: '暴力或威脅', hint: '威脅人身安全、鼓吹暴力' },
  { value: 'SCAM', label: '詐騙或廣告', hint: '金錢詐騙、商業推銷' },
  { value: 'IMPERSONATION', label: '假冒他人', hint: '冒用身分或照片' },
  { value: 'HATE', label: '仇恨言論', hint: '針對特定族群的貶抑' },
  { value: 'SELF_HARM', label: '擔心對方想傷害自己', hint: '我們會優先關心對方，不會讓他知道是誰說的' },
  { value: 'OTHER', label: '其他', hint: '請在下方補充說明' },
]

// 台灣 24 小時求助資源。顯示在自我傷害相關的情境。
export const SUPPORT_LINES = [
  { name: '衛福部安心專線', phone: '1925' },
  { name: '生命線', phone: '1995' },
  { name: '張老師專線', phone: '1980' },
]
