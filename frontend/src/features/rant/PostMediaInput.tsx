// 發文多圖上傳（Threads 風）：最多 MAX_POST_IMAGES 張 + 語音。
// 留言仍用 shared/ui/MediaInput（單圖）；此元件只給說說看發文頁。
import { useRef, useState } from 'react'
import { Image as ImageIcon, Mic, MicOff, X } from 'lucide-react'
import { uploadMedia } from '../../shared/api/media'
import { imageToResizedDataUrl } from '../../shared/lib/mediaTools'
import { useAudioRecorder } from '../../shared/hooks/useAudioRecorder'

export const MAX_POST_IMAGES = 9

export type PostMediaState = {
  imageDataUrls: string[]
  audioDataUrl: string | null
}

export const EMPTY_POST_MEDIA: PostMediaState = { imageDataUrls: [], audioDataUrl: null }

type Props = {
  value: PostMediaState
  onChange: (media: PostMediaState) => void
  folder?: string
}

export default function PostMediaInput({ value, onChange, folder = 'rants' }: Props) {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(false)
  const { isRecording, toggle: toggleRecording } = useAudioRecorder(async (dataUrl) => {
    onChange({ ...value, audioDataUrl: await uploadMedia(dataUrl, folder) })
  })

  const remaining = MAX_POST_IMAGES - value.imageDataUrls.length
  const atLimit = remaining <= 0

  async function handleImagePick(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? []).slice(0, remaining)
    e.target.value = ''
    if (files.length === 0) return
    setUploading(true)
    try {
      // 逐張縮圖 → 上傳，依選取順序加入
      const urls: string[] = []
      for (const file of files) {
        const dataUrl = await imageToResizedDataUrl(file)
        urls.push(await uploadMedia(dataUrl, folder))
      }
      onChange({ ...value, imageDataUrls: [...value.imageDataUrls, ...urls].slice(0, MAX_POST_IMAGES) })
    } finally {
      setUploading(false)
    }
  }

  function removeImage(index: number) {
    onChange({ ...value, imageDataUrls: value.imageDataUrls.filter((_, i) => i !== index) })
  }

  return (
    <div className="post-media-input">
      {/* 圖片縮圖列 */}
      {value.imageDataUrls.length > 0 && (
        <div className="post-media-thumbs">
          {value.imageDataUrls.map((url, i) => (
            <div key={`${url}-${i}`} className="post-media-thumb">
              <img src={url} alt={`圖片 ${i + 1}`} />
              <button type="button" className="post-media-thumb-remove" onClick={() => removeImage(i)} aria-label="移除這張圖片">
                <X size={12} strokeWidth={2.4} />
              </button>
            </div>
          ))}
        </div>
      )}

      {/* 語音預覽 */}
      {value.audioDataUrl && (
        <div className="post-media-audio">
          <audio controls src={value.audioDataUrl} />
          <button type="button" className="post-media-thumb-remove" onClick={() => onChange({ ...value, audioDataUrl: null })} aria-label="移除語音">
            <X size={12} strokeWidth={2.4} />
          </button>
        </div>
      )}

      {/* 工具列 */}
      <div className="post-media-toolbar">
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          multiple
          className="media-file-hidden"
          onChange={handleImagePick}
        />
        <button
          type="button"
          className="media-tool-btn"
          onClick={() => fileInputRef.current?.click()}
          disabled={atLimit || uploading}
          title={atLimit ? `最多 ${MAX_POST_IMAGES} 張` : '加上照片'}
        >
          <ImageIcon size={20} />
        </button>
        <button
          type="button"
          className={`media-tool-btn${isRecording ? ' recording' : ''}`}
          onClick={toggleRecording}
          title={isRecording ? '停止錄音' : '語音留言'}
        >
          {isRecording ? <MicOff size={20} /> : <Mic size={20} />}
        </button>
        {uploading && <span className="recording-hint">上傳中…</span>}
        {!uploading && value.imageDataUrls.length > 0 && (
          <span className="post-media-count">{value.imageDataUrls.length}/{MAX_POST_IMAGES}</span>
        )}
        {isRecording && <span className="recording-hint">錄音中…再按停止</span>}
      </div>
    </div>
  )
}
