import { request } from './httpClient'
import {
  FULL_MAX_SIZE,
  FULL_QUALITY,
  THUMB_MAX_SIZE,
  THUMB_QUALITY,
  imageToResizedDataUrl,
} from '../lib/mediaTools'

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

/**
 * 上傳一張圖片，同時產生並上傳縮圖，回傳兩個 URL。
 * 列表只載 thumbUrl（長邊 640）大幅省頻寬，點開才載 url（長邊 2048）。
 * 縮圖與原圖平行處理，不增加使用者等待時間。
 */
export async function uploadImageWithThumb(
  file: File,
  folder: string,
): Promise<{ url: string; thumbUrl: string }> {
  const [fullDataUrl, thumbDataUrl] = await Promise.all([
    imageToResizedDataUrl(file, FULL_MAX_SIZE, FULL_QUALITY),
    imageToResizedDataUrl(file, THUMB_MAX_SIZE, THUMB_QUALITY),
  ])
  const [url, thumbUrl] = await Promise.all([
    uploadMedia(fullDataUrl, folder),
    uploadMedia(thumbDataUrl, `${folder}/thumbs`),
  ])
  return { url, thumbUrl }
}
