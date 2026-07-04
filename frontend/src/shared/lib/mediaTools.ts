// 媒體共用工具：圖片縮圖、Blob → data URL。發文/留言/聊天的圖片與語音都走這裡。

// 把 Supabase 公開物件 URL 改寫成「即時縮圖」（render/image transform），
// 列表/預覽只載小圖大幅省流量；點開再用原圖。
// 非 Supabase 公開 URL（data URL、passthrough、其他來源）原樣回傳。
export function thumbUrl(url: string, width: number, quality = 70): string {
  if (!url || !url.includes('/storage/v1/object/public/')) return url
  const transformed = url.replace('/storage/v1/object/public/', '/storage/v1/render/image/public/')
  const sep = transformed.includes('?') ? '&' : '?'
  return `${transformed}${sep}width=${width}&quality=${quality}`
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
