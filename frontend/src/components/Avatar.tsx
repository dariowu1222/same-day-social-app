import { useState } from 'react'
import { animalFor } from '../lib/userDisplay'

type Props = {
  photo?: string | null
  seed: string
  className?: string
}

// 頭像：優先顯示真實照片，照片缺失或載入失敗時退回動物 emoji（依 seed 穩定挑選）。
export default function Avatar({ photo, seed, className }: Props) {
  const [failed, setFailed] = useState(false)
  const showPhoto = photo && !failed

  return (
    <span className={`avatar${className ? ` ${className}` : ''}${showPhoto ? ' has-photo' : ''}`}>
      {showPhoto ? (
        <img src={photo} alt="" onError={() => setFailed(true)} />
      ) : (
        <span className="avatar-fallback" aria-hidden>{animalFor(seed)}</span>
      )}
    </span>
  )
}
