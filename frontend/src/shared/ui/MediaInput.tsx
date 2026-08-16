import { useRef } from 'react'
import { Image, Mic, MicOff, X } from 'lucide-react'
import { uploadImageWithThumb, uploadMedia } from '../api/media'
import { useAudioRecorder } from '../hooks/useAudioRecorder'

export type MediaState = {
  imageUrl: string | null
  thumbUrl: string | null
  audioUrl: string | null
}

type Props = {
  value: MediaState
  onChange: (media: MediaState) => void
  folder?: string
}

export const EMPTY_MEDIA: MediaState = { imageUrl: null, thumbUrl: null, audioUrl: null }

export default function MediaInput({ value, onChange, folder = 'rants' }: Props) {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const { isRecording, toggle: toggleRecording } = useAudioRecorder(async (dataUrl) => {
    onChange({ ...value, audioUrl: await uploadMedia(dataUrl, folder) })
  })

  async function handleImagePick(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    const { url, thumbUrl } = await uploadImageWithThumb(file, folder)
    onChange({ ...value, imageUrl: url, thumbUrl })
  }

  return (
    <div className="media-input-wrap">
      {/* 預覽區 */}
      {(value.imageUrl || value.audioUrl) && (
        <div className="media-preview-row">
          {value.imageUrl && (
            <div className="media-preview-item">
              <img src={value.thumbUrl ?? value.imageUrl} className="media-preview-img" alt="預覽" />
              <button type="button" className="media-remove-btn" onClick={() => onChange({ ...value, imageUrl: null, thumbUrl: null })}>
                <X size={11} />
              </button>
            </div>
          )}
          {value.audioUrl && (
            <div className="media-preview-item media-preview-audio-wrap">
              <audio controls src={value.audioUrl} className="media-preview-audio" />
              <button type="button" className="media-remove-btn" onClick={() => onChange({ ...value, audioUrl: null })}>
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
