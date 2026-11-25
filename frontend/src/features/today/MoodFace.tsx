import type { ReactNode } from 'react'

/**
 * 今日頁心情卡上的四張臉。
 *
 * 以前用的是系統表情符號（🌧 🥺 💬 ✨）：每支手機畫出來都不一樣——同一張卡在
 * 華為、三星、iPhone 上是三種畫風，而且四個符號裡只有一個是臉，看不出是一組。
 * 自己畫的話每支手機都一樣，選到的那張還能換底色。
 *
 * 四張臉共用同一個圓底、同一種筆畫粗細，只換五官：
 * 累＝閉眼平嘴、委屈＝八字眉加一滴淚、想說＝張嘴、還不錯＝笑眼加腮紅。
 */
export type MoodKind = 'tired' | 'wronged' | 'talk' | 'good'

const dot = (cx: number, cy: number) => <circle cx={cx} cy={cy} r="2.3" fill="#33241b" stroke="none" />

const FEATURES: Record<MoodKind, ReactNode> = {
  tired: <path d="M12.5 21q3.2 2.6 6.4 0M25.1 21q3.2 2.6 6.4 0M18.5 30.5h7" />,
  wronged: (
    <>
      <path d="M12.5 16.5l6-2.2M31.5 16.5l-6-2.2M17.5 31.5q4.5-3.8 9 0" />
      {dot(16, 22)}
      {dot(28, 22)}
      <path d="M31 25.5q2.4 3.4 0 4.6q-2.4-1.2 0-4.6z" fill="#ffffff" stroke="none" />
    </>
  ),
  talk: (
    <>
      {dot(16, 20)}
      {dot(28, 20)}
      <ellipse cx="22" cy="30" rx="3.4" ry="4" fill="#33241b" stroke="none" />
    </>
  ),
  good: (
    <>
      <path d="M12.5 22q3.2-4.2 6.4 0M25.1 22q3.2-4.2 6.4 0M15.5 28q6.5 6.5 13 0" />
      <circle cx="10" cy="28" r="2.6" fill="#ffffff" opacity="0.5" stroke="none" />
      <circle cx="34" cy="28" r="2.6" fill="#ffffff" opacity="0.5" stroke="none" />
    </>
  ),
}

export default function MoodFace({ kind }: { kind: MoodKind }) {
  return (
    <svg className="mood-face" viewBox="0 0 44 44" aria-hidden="true">
      <circle className="mood-face-bg" cx="22" cy="22" r="20" />
      <g fill="none" stroke="#33241b" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
        {FEATURES[kind]}
      </g>
    </svg>
  )
}
