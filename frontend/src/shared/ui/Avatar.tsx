import { useState } from 'react'

type Props = {
  photo?: string | null
  seed: string
  className?: string
}

// 沒放照片的人底色有三種，依使用者 ID 固定挑一種——聊天列表裡連續幾個人都沒照片時，
// 至少不是一整排一模一樣的圖。
const TINTS = ['#dcebe3', '#e6ecd8', '#d8e8e6']

function tintFor(seed: string): string {
  let hash = 0
  for (let i = 0; i < seed.length; i++) {
    hash = (hash * 31 + seed.charCodeAt(i)) | 0
  }
  return TINTS[Math.abs(hash) % TINTS.length]
}

/**
 * 頭像：有照片就顯示照片；沒有、或照片載入失敗，顯示品牌裡代表「對方」的那顆綠色的圓。
 *
 * 以前沒照片時會去外部網站抓一張隨機風景照（再不行才退回動物表情符號）。
 * 那張風景照看起來像是對方自己放的，其實不是；而且等於把對方的 ID 送給外站。
 * 現在一看就知道「這個人還沒放照片」，也不必連出去。
 */
export default function Avatar({ photo, seed, className }: Props) {
  const [failed, setFailed] = useState(false)
  const showPhoto = photo && !failed

  return (
    <span className={`avatar${className ? ` ${className}` : ''}${showPhoto ? ' has-photo' : ''}`}>
      {showPhoto ? (
        <img src={photo} alt="" onError={() => setFailed(true)} />
      ) : (
        <svg className="avatar-fallback" viewBox="0 0 40 40" aria-hidden="true">
          <rect width="40" height="40" fill={tintFor(seed)} />
          <circle cx="20" cy="22" r="13" fill="#3f7d5e" />
          <ellipse cx="16.4" cy="20" rx="1.6" ry="2.2" fill="#33241b" />
          <ellipse cx="22.6" cy="20" rx="1.6" ry="2.2" fill="#33241b" />
        </svg>
      )}
    </span>
  )
}
