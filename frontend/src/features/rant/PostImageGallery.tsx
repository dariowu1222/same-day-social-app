// 貼文圖片顯示（Threads 風）：單圖大圖；多圖橫向 scroll-snap，露出下一張；點擊開全螢幕檢視。
import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { X } from 'lucide-react'

type Props = { images: string[] }

export default function PostImageGallery({ images }: Props) {
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null)
  if (!images || images.length === 0) return null
  const single = images.length === 1

  return (
    // 阻止冒泡：點圖片開檢視器，不要觸發外層「進貼文詳情」
    <div
      className={`post-gallery${single ? ' post-gallery-single' : ' post-gallery-multi'}`}
      onClick={(e) => e.stopPropagation()}
    >
      {images.map((url, i) => (
        <button key={`${url}-${i}`} type="button" className="post-gallery-item" onClick={() => setLightboxIndex(i)}>
          <img src={url} alt={`圖片 ${i + 1}`} loading="lazy" />
        </button>
      ))}
      {lightboxIndex !== null && (
        <ImageLightbox images={images} startIndex={lightboxIndex} onClose={() => setLightboxIndex(null)} />
      )}
    </div>
  )
}

type LightboxProps = { images: string[]; startIndex: number; onClose: () => void }

function ImageLightbox({ images, startIndex, onClose }: LightboxProps) {
  const scrollerRef = useRef<HTMLDivElement>(null)
  const [current, setCurrent] = useState(startIndex)

  // 開啟時捲到點擊的那張，並鎖背景捲動 + Esc 關閉
  useEffect(() => {
    const el = scrollerRef.current
    if (el) el.scrollLeft = startIndex * el.clientWidth
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKey)
    const prevOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = prevOverflow
    }
  }, [startIndex, onClose])

  function onScroll() {
    const el = scrollerRef.current
    if (!el || el.clientWidth === 0) return
    setCurrent(Math.round(el.scrollLeft / el.clientWidth))
  }

  return createPortal(
    <div className="lightbox" onClick={onClose}>
      <button className="lightbox-close" type="button" onClick={onClose} aria-label="關閉">
        <X size={24} />
      </button>
      {images.length > 1 && <span className="lightbox-counter">{current + 1} / {images.length}</span>}
      <div className="lightbox-scroller" ref={scrollerRef} onScroll={onScroll} onClick={(e) => e.stopPropagation()}>
        {images.map((url, i) => (
          <div key={`${url}-${i}`} className="lightbox-slide">
            <img src={url} alt={`圖片 ${i + 1}`} />
          </div>
        ))}
      </div>
    </div>,
    document.body,
  )
}
