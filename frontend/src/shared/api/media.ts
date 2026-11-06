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
 * 本機開發後端未設定物件儲存時會原樣回傳 data URL（passthrough）。
 *
 * 失敗會往上丟，不再退回原本的 data URL。
 *
 * 原本那個 catch 是這樣寫的：上傳失敗就把整串 base64 當成「網址」回傳，
 * 呼叫端毫不知情，接著它被寫進 users.photo_data_urls、樹洞圖片或聊天訊息。
 * 一張照片的 base64 動輒幾百 KB，而 Supabase 免費方案的資料庫只有 500 MB；
 * 之後每次查詢都要把它搬一次——配對的全表載入、樹洞列表、聊天列表的頭像。
 * 線上的 chat_messages 現在就有三則整串 base64 的訊息，最長 120 KB。
 *
 * 後端現在也不收 data: 開頭的值（MediaUrlPolicy），所以就算這裡繼續吞錯，
 * 使用者也只會看到一句看不懂的「圖片必須是從這個 App 上傳的」。
 */
export async function uploadMedia(dataUrl: string, folder: string): Promise<string> {
  if (!dataUrl || !dataUrl.startsWith('data:')) return dataUrl
  const res = await request<{ url: string }>('/api/media', {
    method: 'POST',
    body: JSON.stringify({ dataUrl, folder }),
  })
  if (!res.data?.url) throw new Error('上傳失敗，請再試一次。')
  return res.data.url
}

/** 上傳失敗時要對使用者說的那一句。 */
export function uploadErrorMessage(err: unknown): string {
  return err instanceof Error && err.message ? err.message : '上傳失敗，請再試一次。'
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
