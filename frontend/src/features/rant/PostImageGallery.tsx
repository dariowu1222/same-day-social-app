// 貼文圖片顯示（Threads 風）：單圖大圖；多圖橫向 scroll-snap，露出下一張。
// 手機原生左右滑動；桌機（有 hover）另外顯示左右箭頭。點圖開全螢幕檢視，全螢幕同樣可滑動/箭頭切換。
import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import ImageLightbox from '../../shared/ui/ImageLightbox'
import type { PostImage } from './types'

type Props = { images: PostImage[] }

export default function PostImageGallery({ images }: Props) {
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null)
  if (!images || images.length === 0) return null
  const single = images.length === 1

  // 阻止冒泡：點圖片開檢視器，不要觸發外層「進貼文詳情」
  const stop = (e: React.MouseEvent) => e.stopPropagation()

  return (
    <div className={`post-gallery${single ? ' post-gallery-single' : ''}`} onClick={stop}>
      {single ? (
        <button type="button" className="post-gallery-item" onClick={() => setLightboxIndex(0)}>
          <img src={images[0].thumb} alt="圖片 1" loading="lazy" />
        </button>
      ) : (
        <HScroller className="post-gallery-scroller" navClassName="gallery-nav">
          {images.map((img, i) => (
            <button key={`${img.full}-${i}`} type="button" className="post-gallery-item" onClick={() => setLightboxIndex(i)}>
              <img src={img.thumb} alt={`圖片 ${i + 1}`} loading="lazy" />
            </button>
          ))}
        </HScroller>
      )}

      {lightboxIndex !== null && (
        <ImageLightbox
          images={images.map((img) => img.full)}
          startIndex={lightboxIndex}
          onClose={() => setLightboxIndex(null)}
        />
      )}
    </div>
  )
}

// 橫向捲動容器 + 左右箭頭（依可捲動方向顯示/隱藏；箭頭在桌機才顯示，手機靠原生滑動）
type HScrollerProps = {
  children: React.ReactNode
  className: string
  navClassName: string
}
function HScroller({ children, className, navClassName }: HScrollerProps) {
  const ref = useRef<HTMLDivElement>(null)
  const [canLeft, setCanLeft] = useState(false)
  const [canRight, setCanRight] = useState(false)

  function update() {
    const el = ref.current
    if (!el) return
    setCanLeft(el.scrollLeft > 4)
    setCanRight(el.scrollLeft + el.clientWidth < el.scrollWidth - 4)
  }
  useLayoutEffect(update, [])
  useEffect(() => {
    const el = ref.current
    window.addEventListener('resize', update)
    // 圖片是 width:auto，載入後寬度才確定 → 用 capture 監聽底下 img 的 load 再重算箭頭
    el?.addEventListener('load', update, true)
    return () => {
      window.removeEventListener('resize', update)
      el?.removeEventListener('load', update, true)
    }
  }, [])

  // 捲到「相鄰一張圖的邊界」＝ scroll-snap 的 snap point；
  // 否則 mandatory snap 會把不到一張的捲動量 snap 回原位（箭頭看起來沒反應）。
  function go(dir: 1 | -1) {
    const el = ref.current
    if (!el) return
    const base = el.getBoundingClientRect().left
    const edges = [...el.children].map((c) => c.getBoundingClientRect().left - base + el.scrollLeft)
    const cur = el.scrollLeft
    let target: number | undefined
    if (dir > 0) target = edges.find((x) => x > cur + 2)
    else { const before = edges.filter((x) => x < cur - 2); target = before.length ? before[before.length - 1] : 0 }
    // 用 instant 設定 scrollLeft（可靠），動畫交給 CSS scroll-behavior: smooth
    if (target != null) el.scrollLeft = target
  }

  return (
    <div className={`${navClassName}-wrap`}>
      <div className={className} ref={ref} onScroll={update}>
        {children}
      </div>
      {canLeft && (
        <button type="button" className={`${navClassName} ${navClassName}-left`} onClick={() => go(-1)} aria-label="上一張">
          <ChevronLeft size={20} />
        </button>
      )}
      {canRight && (
        <button type="button" className={`${navClassName} ${navClassName}-right`} onClick={() => go(1)} aria-label="下一張">
          <ChevronRight size={20} />
        </button>
      )}
    </div>
  )
}
