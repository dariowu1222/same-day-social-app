// 個人資料欄位的預設選項（「我的」頁與 onboarding 共用）

// 只留男/女，不是為了省事，是因為配對過濾只認這兩個值：
// 「想認識的對象」只有男生/女生/不限，而 MatchService.MatchesPreferences 把
// 前兩者翻成「男」「女」去比對。填「其他」或「不公開」的人在任何指定性別的
// 搜尋裡都會被濾掉，等於自願退出大部分牌堆，而 app 裡沒有任何地方會告訴他。
// 與其留一個看起來可選、實際上會讓人隱形的選項，不如不提供。
export const GENDER_OPTIONS = ['男', '女'] as const
export const RELATIONSHIP_OPTIONS = ['單身', '曖昧中', '穩定交往', '已婚', '不公開'] as const
export const DATING_GOAL_OPTIONS = ['交友', '約會', '長期關係', '先聊聊', '想抒發'] as const
export const LOOKING_FOR_OPTIONS = ['男生', '女生', '不限'] as const
export const PERSONALITY_OPTIONS = [
  '內向', '外向', '慢熟', '健談', '理性', '感性',
  '幽默', '文靜', '隨和', '直率', '細心', '樂觀',
]
export const APPEARANCE_OPTIONS = [
  '鄰家感', '陽光', '文青', '酷帥', '可愛', '成熟', '中性', '運動風', '氣質',
]
export const BLOOD_OPTIONS = ['A', 'B', 'O', 'AB', '其他', '不確定'] as const

// 縣市清單：「所在縣市」與「偏好縣市」共用。
//
// 必須和後端的 TaiwanAreas（backend/Domain/TaiwanAreas.cs）一字不差：後端只收
// 清單裡的值，配對也拿它逐字比對。這裡多一個選項，就是一個選得到、存不進去的選項。
export const AREA_OPTIONS = [
  '台北市', '新北市', '基隆市', '桃園市', '新竹市', '新竹縣',
  '苗栗縣', '台中市', '彰化縣', '南投縣', '雲林縣', '嘉義市',
  '嘉義縣', '台南市', '高雄市', '屏東縣', '宜蘭縣', '花蓮縣',
  '台東縣', '澎湖縣', '金門縣', '連江縣',
] as const

// 距離偏好的兩種方式，擇一。
// 距離是「縣市對縣市」的估算（同縣市算 0），不是定位——這個 App 不向使用者要位置。
export type DistanceMode = 'km' | 'area'
export const DISTANCE_MODE_LABELS = { km: '依距離', area: '依縣市' } as const
export const DISTANCE_KM_MIN = 1
export const DISTANCE_KM_MAX = 500
