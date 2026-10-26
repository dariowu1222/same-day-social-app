import { useRef, useState } from 'react'
import { blobToDataUrl } from '../lib/mediaTools'

// 語音錄音共用 hook：toggle 開始/停止，停止後回傳錄好的 data URL。
export function useAudioRecorder(onRecorded: (dataUrl: string) => void) {
  const [isRecording, setIsRecording] = useState(false)
  const recorderRef = useRef<MediaRecorder | null>(null)
  const chunksRef = useRef<Blob[]>([])

  async function toggle() {
    if (isRecording) {
      recorderRef.current?.stop()
      setIsRecording(false)
      return
    }
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      const recorder = new MediaRecorder(stream)
      chunksRef.current = []
      recorder.ondataavailable = (e) => { if (e.data.size > 0) chunksRef.current.push(e.data) }
      recorder.onstop = async () => {
        // 用 recorder 實際採用的格式，不要寫死 audio/webm：
        // iOS 的 WKWebView 錄出來是 mp4/aac，標錯會讓上傳後的副檔名與
        // 播放端的判斷對不上。Android 本來就是 webm，行為不變。
        const type = recorder.mimeType || 'audio/webm'
        const blob = new Blob(chunksRef.current, { type })
        onRecorded(await blobToDataUrl(blob))
        stream.getTracks().forEach((t) => t.stop())
      }
      recorderRef.current = recorder
      recorder.start()
      setIsRecording(true)
    } catch {
      alert('無法取得麥克風權限')
    }
  }

  return { isRecording, toggle }
}
