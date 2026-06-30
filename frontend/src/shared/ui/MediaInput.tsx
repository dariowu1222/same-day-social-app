import { useRef } from 'react'
import { Image, Mic, MicOff, X } from 'lucide-react'
import { uploadMedia } from '../api/media'
import { imageToResizedDataUrl } from '../lib/mediaTools'
import { useAudioRecorder } from '../hooks/useAudioRecorder'

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

export default function MediaInput({ value, onChange, folder = 'rants' }: Props) {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const { isRecording, toggle: toggleRecording } = useAudioRecorder(async (dataUrl) => {
    onChange({ ...value, audioDataUrl: await uploadMedia(dataUrl, folder) })
  })

  async function handleImagePick(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    const dataUrl = await imageToResizedDataUrl(file)
    const url = await uploadMedia(dataUrl, folder)
    onChange({ ...value, imageDataUrl: url })
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
