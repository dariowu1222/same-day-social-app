// 全螢幕圖片檢視器：點圖開啟，可左右滑動/箭頭切換、Esc 關閉。
// 由樹洞貼文（多圖）與回應（單圖）共用；一律載原圖，不用縮圖。
import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { ChevronLeft, ChevronRight, X } from 'lucide-react'

type Props = { images: string[]; startIndex: number; onClose: () => void }

export default function ImageLightbox({ images, startIndex, onClose }: Props) {
  const scrollerRef = useRef<HTMLDivElement>(null)
  const [current, setCurrent] = useState(startIndex)
  const multi = images.length > 1

  // 開啟時捲到點擊的那張，鎖背景捲動 + 鍵盤（Esc 關閉、左右切換）
  useEffect(() => {
    const el = scrollerRef.current
    if (el) el.scrollLeft = startIndex * el.clientWidth
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
      if (e.key === 'ArrowRight') step(1)
      if (e.key === 'ArrowLeft') step(-1)
    }
    document.addEventListener('keydown', onKey)
    const prevOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = prevOverflow
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [startIndex, onClose])

  function onScroll() {
    const el = scrollerRef.current
    if (!el || el.clientWidth === 0) return
    setCurrent(Math.round(el.scrollLeft / el.clientWidth))
  }
  function step(dir: 1 | -1) {
    const el = scrollerRef.current
    // instant：smooth + mandatory snap + onScroll 重繪會互相取消，instant 設定最可靠
    if (el) el.scrollLeft += dir * el.clientWidth
  }

  return createPortal(
    <div className="lightbox" onClick={onClose}>
      <button className="lightbox-close" type="button" onClick={onClose} aria-label="關閉">
        <X size={24} />
      </button>
      {multi && <span className="lightbox-counter">{current + 1} / {images.length}</span>}
      <div className="lightbox-scroller" ref={scrollerRef} onScroll={onScroll} onClick={(e) => e.stopPropagation()}>
        {images.map((url, i) => (
          <div key={`${url}-${i}`} className="lightbox-slide">
            <img src={url} alt={`圖片 ${i + 1}`} />
          </div>
        ))}
      </div>
      {multi && current > 0 && (
        <button type="button" className="lightbox-nav lightbox-nav-left" onClick={(e) => { e.stopPropagation(); step(-1) }} aria-label="上一張">
          <ChevronLeft size={28} />
        </button>
      )}
      {multi && current < images.length - 1 && (
        <button type="button" className="lightbox-nav lightbox-nav-right" onClick={(e) => { e.stopPropagation(); step(1) }} aria-label="下一張">
          <ChevronRight size={28} />
        </button>
      )}
    </div>,
    document.body,
  )
}
