import { useRef, useState } from 'react'

export type MediaState = {
  imageDataUrl: string | null
  audioDataUrl: string | null
}

type Props = {
  value: MediaState
  onChange: (media: MediaState) => void
}

export const EMPTY_MEDIA: MediaState = { imageDataUrl: null, audioDataUrl: null }

export default function MediaInput({ value, onChange }: Props) {
  const [isRecording, setIsRecording] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const recorderRef = useRef<MediaRecorder | null>(null)
  const chunksRef = useRef<Blob[]>([])

  function handleImagePick(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onloadend = () => onChange({ ...value, imageDataUrl: reader.result as string })
    reader.readAsDataURL(file)
    e.target.value = ''
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
      recorder.onstop = () => {
        const blob = new Blob(chunksRef.current, { type: 'audio/webm' })
        const reader = new FileReader()
        reader.onloadend = () => onChange({ ...value, audioDataUrl: reader.result as string })
        reader.readAsDataURL(blob)
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
    <div className="media-input-bar">
      {/* 照片 */}
      <input ref={fileInputRef} type="file" accept="image/*" className="media-file-hidden" onChange={handleImagePick} />
      <button type="button" className="media-icon-btn" onClick={() => fileInputRef.current?.click()} title="加上照片">
        📷
      </button>

      {/* 錄音 */}
      <button
        type="button"
        className={`media-icon-btn${isRecording ? ' recording' : ''}`}
        onClick={toggleRecording}
        title={isRecording ? '停止錄音' : '開始錄音'}
      >
        {isRecording ? '⏹' : '🎙'}
      </button>
      {isRecording && <span className="recording-hint">錄音中…點⏹停止</span>}

      {/* 預覽 */}
      {value.imageDataUrl && (
        <div className="media-preview-item">
          <img src={value.imageDataUrl} className="media-preview-img" alt="預覽" />
          <button type="button" className="media-remove-btn" onClick={() => onChange({ ...value, imageDataUrl: null })}>×</button>
        </div>
      )}
      {value.audioDataUrl && (
        <div className="media-preview-item">
          <audio controls src={value.audioDataUrl} className="media-preview-audio" />
          <button type="button" className="media-remove-btn" onClick={() => onChange({ ...value, audioDataUrl: null })}>×</button>
        </div>
      )}
    </div>
  )
}
