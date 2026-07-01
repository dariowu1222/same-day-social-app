// 媒體共用工具：圖片縮圖、Blob → data URL。發文/留言/聊天的圖片與語音都走這裡。

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
