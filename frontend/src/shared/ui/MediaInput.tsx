import { useRef, useState } from 'react'
import { Image, Mic, MicOff, X } from 'lucide-react'
import { uploadMedia } from '../api/media'

export type MediaState = {
  imageDataUrl: string | null
  audioDataUrl: string | null
}

type Props = {
  value: MediaState
  onChange: (media: MediaState) => void
  folder?: string
}

export const EMPTY_MEDIA: MediaState = { imageDataUrl: null, audioDataUrl: null }

function readAsDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onloadend = () => resolve(reader.result as string)
    reader.onerror = reject
    reader.readAsDataURL(blob)
  })
}

// 圖片縮圖 → data URL（控制體積，避免超過物件儲存上限）
function imageToResizedDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const img = new window.Image()
    const objUrl = URL.createObjectURL(file)
    img.onload = () => {
      URL.revokeObjectURL(objUrl)
      const maxSize = 1280
      const scale = Math.min(1, maxSize / Math.max(img.width, img.height))
      const w = Math.round(img.width * scale)
      const h = Math.round(img.height * scale)
      const canvas = document.createElement('canvas')
      canvas.width = w
      canvas.height = h
      canvas.getContext('2d')!.drawImage(img, 0, 0, w, h)
      resolve(canvas.toDataURL('image/jpeg', 0.85))
    }
    img.onerror = reject
    img.src = objUrl
  })
}

export default function MediaInput({ value, onChange, folder = 'rants' }: Props) {
  const [isRecording, setIsRecording] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const recorderRef = useRef<MediaRecorder | null>(null)
  const chunksRef = useRef<Blob[]>([])

  async function handleImagePick(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    const dataUrl = await imageToResizedDataUrl(file)
    const url = await uploadMedia(dataUrl, folder)
    onChange({ ...value, imageDataUrl: url })
  }

  async function toggleRecording() {
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
        const blob = new Blob(chunksRef.current, { type: 'audio/webm' })
        const dataUrl = await readAsDataUrl(blob)
        const url = await uploadMedia(dataUrl, folder)
        onChange({ ...value, audioDataUrl: url })
        stream.getTracks().forEach((t) => t.stop())
      }
      recorderRef.current = recorder
      recorder.start()
      setIsRecording(true)
    } catch {
      alert('無法取得麥克風權限')
    }
  }

  return (
    <div className="media-input-wrap">
      {/* 預覽區 */}
      {(value.imageDataUrl || value.audioDataUrl) && (
        <div className="media-preview-row">
          {value.imageDataUrl && (
            <div className="media-preview-item">
              <img src={value.imageDataUrl} className="media-preview-img" alt="預覽" />
              <button type="button" className="media-remove-btn" onClick={() => onChange({ ...value, imageDataUrl: null })}>
                <X size={11} />
              </button>
            </div>
          )}
          {value.audioDataUrl && (
            <div className="media-preview-item media-preview-audio-wrap">
              <audio controls src={value.audioDataUrl} className="media-preview-audio" />
              <button type="button" className="media-remove-btn" onClick={() => onChange({ ...value, audioDataUrl: null })}>
                <X size={11} />
              </button>
            </div>
          )}
        </div>
      )}

      {/* 工具列 */}
      <div className="media-toolbar">
        <input ref={fileInputRef} type="file" accept="image/*" className="media-file-hidden" onChange={handleImagePick} />
        <button
          type="button"
          className="media-tool-btn"
          onClick={() => fileInputRef.current?.click()}
          title="加上照片"
        >
          <Image size={20} />
        </button>

        <button
          type="button"
          className={`media-tool-btn${isRecording ? ' recording' : ''}`}
          onClick={toggleRecording}
          title={isRecording ? '停止錄音' : '語音留言'}
        >
          {isRecording ? <MicOff size={20} /> : <Mic size={20} />}
        </button>

        {isRecording && <span className="recording-hint">錄音中…再按停止</span>}
      </div>
    </div>
  )
}
