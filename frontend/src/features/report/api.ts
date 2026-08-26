import { request } from '../../shared/api/httpClient'
import type { ReportCategory, ReportTargetType } from './types'

export type SubmitReportResponse = {
  reportId: string
  alreadyReported: boolean
  selfHarmSupport: boolean
  message: string
}

/** 統一檢舉入口：貼文、回應、聊天訊息、使用者都走這裡。 */
export async function submitReport(payload: {
  targetType: ReportTargetType
  targetId: string
  category: ReportCategory
  detail?: string | null
}) {
  return request<SubmitReportResponse>('/api/reports', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}
