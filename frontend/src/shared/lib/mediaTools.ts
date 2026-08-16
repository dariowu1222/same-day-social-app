// 媒體共用工具：圖片縮圖、Blob → data URL。發文/留言/聊天的圖片與語音都走這裡。

// 圖片尺寸策略：上傳時就產兩張，列表載縮圖、點開才載原圖。
// 註：Supabase 即時縮圖（/storage/v1/render/image/）是 Pro 方案功能，
// 免費方案會回 404 導致列表破圖，所以不依賴即時轉檔，改為上傳時另存縮圖。
export const FULL_MAX_SIZE = 2048
export const FULL_QUALITY = 0.9
export const THUMB_MAX_SIZE = 640
export const THUMB_QUALITY = 0.7

// 顯示用：優先縮圖，沒有縮圖的舊資料回退原圖，確保永遠有圖可顯示。
export function displayUrl(url: string, thumb?: string | null): string {
  return thumb && thumb.length > 0 ? thumb : url
}


export function blobToDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onloadend = () => resolve(reader.result as string)
    reader.onerror = reject
    reader.readAsDataURL(blob)
  })
}

// 圖片縮圖 → JPEG data URL。長邊上限 2048、品質 0.9：兼顧全螢幕清晰度與檔案大小。
export function imageToResizedDataUrl(file: File, maxSize = 2048, quality = 0.9): Promise<string> {
  return new Promise((resolve, reject) => {
    const img = new window.Image()
    const objUrl = URL.createObjectURL(file)
    img.onload = () => {
      URL.revokeObjectURL(objUrl)
      const scale = Math.min(1, maxSize / Math.max(img.width, img.height))
      const w = Math.round(img.width * scale)
      const h = Math.round(img.height * scale)
      const canvas = document.createElement('canvas')
      canvas.width = w
      canvas.height = h
      canvas.getContext('2d')!.drawImage(img, 0, 0, w, h)
      resolve(canvas.toDataURL('image/jpeg', quality))
    }
    img.onerror = reject
    img.src = objUrl
  })
}
