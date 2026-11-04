// 配對卡片上那排標籤的中文對照。
//
// 為什麼需要：後端的 shared_tags 存的是 enum 名稱（WORK_STRESS、TIRED、
// RESPONSE_COMPATIBLE…），MatchCard 以前直接 render，所以使用者看到的是一排
// 英文大寫。標籤本身是給人看的東西，不該長成那樣。
//
// 為什麼不在後端轉：shared_tags 會落地到 matches.shared_tags，存中文的話
// 之後要用它做任何統計或篩選都得反查，而且改文案等於改資料。代號留在資料庫，
// 顯示層才轉。
//
// 認不得的代號原樣輸出，不要回空字串——漏一個標籤比露出一個代號更難發現。

const TAG_LABELS: Record<string, string> = {
  // EventType
  WORK_STRESS: '工作壓力',
  WORK: '工作',
  FAMILY: '家庭',
  FRIENDSHIP: '朋友',
  RELATIONSHIP: '感情',
  STUDY: '學業',
  MONEY: '金錢',
  HEALTH: '健康',
  SMALL_HAPPINESS: '小確幸',
  RANDOM: '說不上來',

  // EmotionTag
  TIRED: '累',
  ANGRY: '生氣',
  SAD: '難過',
  LONELY: '孤單',
  ANXIOUS: '焦慮',
  HAPPY: '開心',
  EXCITED: '期待',
  WRONGED: '委屈',
  CALM: '平靜',

  // 價值觀
  FAIRNESS: '公平',
  RESPECT: '被尊重',
  GENTLE_COMPANY: '溫和陪伴',
  FINANCIAL_STABILITY: '經濟穩定',

  // 興趣
  MOVIE: '影視',
  FOOD: '飲食',
  EXERCISE: '運動',
  CODING: '程式',
  LEARNING: '學習',

  // 計分時加的合成標籤，不是來自任何 enum
  RESPONSE_COMPATIBLE: '聊天節奏合',
}

export function tagLabel(code: string): string {
  return TAG_LABELS[code] ?? code
}
