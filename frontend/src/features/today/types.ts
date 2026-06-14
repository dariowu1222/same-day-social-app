export type TodayAnalysis = {
  eventType: string
  emotionTags: string[]
  valueTags: string[]
  interestTags: string[]
  responseMode: string
}

export type TodayEntry = TodayAnalysis & {
  id: string
  userId: string
  content: string
  visibility: string
  createdAt: string
}
