import { useState, useEffect, useCallback, useLayoutEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { Plus, Search } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { deleteRant, deleteReply, getRants, likeReply, replyRant, understandRant, unlikeRant, unlikeReply, RANT_PAGE_SIZE } from './api'
import type { RantPost } from './types'
import { EMPTY_MEDIA, type MediaState } from '../../shared/ui/MediaInput'
import RantPostCard from './RantPostCard'

// 這張卡片是在哪個容器裡捲的。手機是整個視窗，桌面版面可能是內層的欄位——
// 不寫死，往上找第一個真的會捲的祖先。
function scrollerOf(el: HTMLElement): Window | HTMLElement {
  for (let p = el.parentElement; p; p = p.parentElement) {
    const overflowY = getComputedStyle(p).overflowY
    if ((overflowY === 'auto' || overflowY === 'scroll') && p.scrollHeight > p.clientHeight) return p
  }
  return window
}

export default function RantBoardPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [posts, setPosts] = useState<RantPost[]>([])
  const [hasMore, setHasMore] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const [replies, setReplies] = useState<Record<string, string>>({})
  const [replyMedia, setReplyMedia] = useState<Record<string, MediaState>>({})
  // 同時只展開一則的回應區。開另一則，前一則自動收起。
  const [openPostId, setOpenPostId] = useState<string | null>(null)
  // 開著的那一則，它的留言框現在看不看得到（見下面的 effect）
  const [composerVisible, setComposerVisible] = useState(false)
  // 沒有任何一則開著就一定不用讓——所以這個值是算出來的，不是另外記的。
  // 另外記的話，收起來那一刻還得記得把它改回 false。
  const fabAway = openPostId !== null && composerVisible
  // 換開另一則之前，被點的那張卡片在畫面上的位置
  const anchor = useRef<{ id: string; top: number } | null>(null)
  const listRef = useRef<HTMLElement>(null)
  const padTimer = useRef<number | undefined>(undefined)

  // 互動後重新載入第一頁（回到最新）
  const loadPosts = useCallback(async () => {
    const res = await getRants()
    setPosts(res.data)
    setHasMore(res.data.length === RANT_PAGE_SIZE)
  }, [])

  useEffect(() => { void loadPosts() }, [loadPosts])

  async function loadMore() {
    if (loadingMore || !hasMore || posts.length === 0) return
    setLoadingMore(true)
    try {
      const cursor = posts[posts.length - 1].createdAt
      const res = await getRants(cursor)
      setPosts((prev) => [...prev, ...res.data])
      setHasMore(res.data.length === RANT_PAGE_SIZE)
    } finally {
      setLoadingMore(false)
    }
  }

  // 互動後把後端回傳的那一篇就地換掉，不要整頁重抓。
  // 先前按一次愛心是「1 次寫入 + 1 次全列表重抓」，而且會把使用者
  // 捲到第 2 頁之後的內容整個丟掉、跳回第一頁。
  async function updatePost(action: () => Promise<{ data: RantPost }>) {
    const res = await action()
    setPosts((prev) => prev.map((p) => (p.id === res.data.id ? res.data : p)))
  }

  async function removePost(rantId: string, action: () => Promise<unknown>) {
    await action()
    setPosts((prev) => prev.filter((p) => p.id !== rantId))
  }

  function toggleThread(postId: string, open: boolean) {
    if (!open) {
      setOpenPostId((current) => (current === postId ? null : current))
      return
    }
    // 另一則正開著：先記下被點的這張現在在畫面上的哪裡，換完再把它擺回去。
    const el = openPostId && openPostId !== postId ? document.getElementById(`rant-${postId}`) : null
    anchor.current = el ? { id: postId, top: el.getBoundingClientRect().top } : null
    setOpenPostId(postId)
  }

  // 前一則如果在被點的這則「上面」，它一收起來，底下的東西會整個往上縮——
  // 手指底下那張卡片就跑掉了，最糟是直接縮出畫面。
  //
  // 所以被擠掉的那則不播收合動畫、當場消失（RantPostCard 只有自己按收起才播），
  // 這裡在畫面還沒畫出來之前把捲動位置補回同樣的量。useLayoutEffect 而不是
  // useEffect：等到 effect 才補，使用者會先看到一格跳掉的畫面。
  // 補的時候指定 instant：全站如果哪天開了 scroll-behavior: smooth，這一下會變成
  // 慢慢滑過去，等於把剛才要避免的那個跳動用動畫重播一次。
  useLayoutEffect(() => {
    const before = anchor.current
    anchor.current = null
    if (!before) return
    const el = document.getElementById(`rant-${before.id}`)
    if (!el) return
    const delta = el.getBoundingClientRect().top - before.top
    if (delta === 0) return

    // 捲到最底的時候還有一個坑：前一則收掉之後整頁變短，要補回去的那個位置
    // 可能已經超出「能捲到的最底」，瀏覽器會把捲動量截短——被點的卡片就往下掉
    // 一截（實測是一列的高度，46px 左右）。新開的這則接著會長出來把頁面撐回去，
    // 所以只要在它長出來之前，先在列表底下墊上不夠的那一段，等它長完再拿掉。
    const scroller = scrollerOf(el)
    const box = scroller === window ? document.documentElement : (scroller as HTMLElement)
    const top = scroller === window ? window.scrollY : box.scrollTop
    const view = scroller === window ? window.innerHeight : box.clientHeight
    const short = Math.ceil(top + delta + view - box.scrollHeight)
    const list = listRef.current
    if (short > 0 && list) {
      list.style.paddingBottom = `${short}px`
      window.clearTimeout(padTimer.current)
      padTimer.current = window.setTimeout(() => { list.style.paddingBottom = '' }, 400)
    }

    scroller.scrollBy({ top: delta, behavior: 'instant' })
  }, [openPostId])

  useEffect(() => () => window.clearTimeout(padTimer.current), [])

  // 畫面上有留言框的時候，右下角的發文鈕先讓開——它會蓋在留言框的送出鈕上。
  //
  // 判斷的是「那個留言框現在看不看得到」，不是「有沒有哪一則開著」。
  // 後者會出事：開了一則之後往下滑走，發文鈕就一直不見，要回頭把那則收起來
  // 才找得回來。照現在的寫法，留言框一離開畫面，發文鈕就回來了。
  // rootMargin 底部扣掉 64px：那一截被底部導覽蓋著，不算看得到。
  useEffect(() => {
    const bar = openPostId
      ? document.getElementById(`rant-${openPostId}`)?.querySelector('.reply-form-bar')
      : null
    if (!bar) return
    // observe 之後一定會先回報一次現況，所以換開另一則時不必自己先歸零
    const observer = new IntersectionObserver(
      ([entry]) => setComposerVisible(entry.isIntersecting),
      { rootMargin: '0px 0px -64px 0px' },
    )
    observer.observe(bar)
    return () => observer.disconnect()
  }, [openPostId])

  if (!user) return null

  return (
    <div className="page rant-page">

      {/* 13.1 搜尋入口（sticky）：只作為入口，點擊進入獨立搜尋頁 */}
      <div className="rant-search-entry-bar">
        <button className="rant-search-entry" type="button" onClick={() => navigate('/rant/search')}>
          <Search size={16} className="rant-search-entry-icon" />
          <span className="rant-search-entry-placeholder">搜尋樹洞的心事</span>
        </button>
      </div>

      <p className="rant-board-title">這裡可以說說今天不太想放在心裡的事</p>

      <section className="list" ref={listRef}>
        {posts.map((post) => (
          <RantPostCard
            key={post.id}
            post={post}
            open={openPostId === post.id}
            onToggle={(open) => toggleThread(post.id, open)}
            replyText={replies[post.id] ?? ''}
            onReplyTextChange={(value) => setReplies({ ...replies, [post.id]: value })}
            onUnderstand={(like) => updatePost(() => (like ? understandRant(post.id) : unlikeRant(post.id)))}
            replyMedia={replyMedia[post.id] ?? EMPTY_MEDIA}
            onReplyMediaChange={(media) => setReplyMedia({ ...replyMedia, [post.id]: media })}
            onReply={async (parentReplyId) => {
              await updatePost(() => replyRant(post.id, { content: replies[post.id] ?? '', ...(replyMedia[post.id] ?? EMPTY_MEDIA), parentReplyId }))
              // 送出成功才清。以前送出後留言框會整個收掉，所以沒清也看不出來——
              // 但草稿其實還留著，下次打開同一則會看到上一句話還在框裡。
              // 現在留言框送出後不收，不清的話同一句話按兩下就送兩次。
              setReplies((prev) => ({ ...prev, [post.id]: '' }))
              setReplyMedia((prev) => ({ ...prev, [post.id]: EMPTY_MEDIA }))
            }}
            onDelete={() => removePost(post.id, () => deleteRant(post.id))}
            // 回應的愛心、收回都只換掉這一篇。以前按回應的愛心是整頁重抓第一頁，
            // 滑到第二頁之後按一下就被丟回最上面。
            onLikeReply={(replyId, like) => updatePost(() => (like ? likeReply(post.id, replyId) : unlikeReply(post.id, replyId)))}
            onDeleteReply={(replyId) => updatePost(() => deleteReply(post.id, replyId))}
            onHashtagClick={(tag) => navigate('/rant/search', { state: { q: tag } })}
          />
        ))}

        {hasMore && posts.length > 0 && (
          <button className="rant-load-more" type="button" onClick={loadMore} disabled={loadingMore}>
            {loadingMore ? '載入中⋯' : '載入更多'}
          </button>
        )}
      </section>

      {/* FAB：右下角發文按鈕 */}
      <button
        className={`rant-fab${fabAway ? ' is-away' : ''}`}
        onClick={() => navigate('/rant/new')}
        aria-label="說說看"
      >
        <Plus size={24} />
      </button>
    </div>
  )
}
