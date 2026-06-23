import { request } from './httpClient'

/**
 * 上傳媒體（data URL）到物件儲存，回傳可公開存取的 URL。
 * 後端未設定物件儲存時會原樣回傳 data URL（passthrough）；
 * 上傳失敗時也退回原 data URL，避免使用者流程中斷。
 */
export async function uploadMedia(dataUrl: string, folder: string): Promise<string> {
  if (!dataUrl || !dataUrl.startsWith('data:')) return dataUrl
  try {
    const res = await request<{ url: string }>('/api/media', {
      method: 'POST',
      body: JSON.stringify({ dataUrl, folder }),
    })
    return res.data?.url || dataUrl
  } catch {
    return dataUrl
  }
}
