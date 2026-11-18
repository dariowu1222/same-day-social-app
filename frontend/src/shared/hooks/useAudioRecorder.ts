import { useRef, useState } from 'react'
import { blobToDataUrl } from '../lib/mediaTools'

// 語音錄音共用 hook：toggle 開始/停止，停止後回傳錄好的 data URL 與長度。
//
// 為什麼要自己量長度：MediaRecorder 是邊錄邊寫，錄完才知道多長，所以產出的
// WebM 標頭裡沒有 Duration。瀏覽器讀這種檔案時 audio.duration 會是 Infinity，
// 原生 <audio controls> 的進度條算不出比例，一播放就直接頂到底。
// 從 HTTP 取回的同一個檔案沒這個問題——可以 range request，瀏覽器掃得到結尾；
// 所以只有「還沒上傳的草稿」會踩到，而那正是我們自己知道長度的時候。
export function useAudioRecorder(onRecorded: (dataUrl: string, durationMs: number) => void) {
  const [isRecording, setIsRecording] = useState(false)
  const recorderRef = useRef<MediaRecorder | null>(null)
  const chunksRef = useRef<Blob[]>([])
  const startedAtRef = useRef(0)

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
        onRecorded(await blobToDataUrl(blob), Date.now() - startedAtRef.current)
        stream.getTracks().forEach((t) => t.stop())
      }
      recorderRef.current = recorder
      startedAtRef.current = Date.now()
      recorder.start()
      setIsRecording(true)
    } catch {
      alert('無法取得麥克風權限')
    }
  }

  return { isRecording, toggle }
}
