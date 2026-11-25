import { useSyncExternalStore } from 'react'
import type { RantReply } from './types'

/**
 * 回應的排序。
 *
 * 三種依據，各有正反兩個方向：
 *  ・時間：最早的在前（預設，照對話發生的順序讀）／最新的在前
 *  ・愛心：這一則拿到幾顆愛心
 *  ・熱門：愛心數加上「底下有幾則回應」——有人接話也算一種熱度，不只是被按愛心
 *
 * 只排第一層。回應裡的回應永遠照時間：那是一段對話，打亂了就讀不懂誰在回誰。
 *
 * 排序在前端做：點開一篇時回應本來就整包在手上，不必為了換個順序再問一次後端。
 */
export type ReplySortKey = 'time' | 'hearts' | 'hot'
export type ReplySort = { key: ReplySortKey; desc: boolean }

export const DEFAULT_REPLY_SORT: ReplySort = { key: 'time', desc: false }

/** 選單上的六個選項，依序。label 也是排序那顆按鈕上顯示的字。 */
export const REPLY_SORT_OPTIONS: { sort: ReplySort; label: string; hint?: string; group: ReplySortKey }[] = [
  { sort: { key: 'time', desc: false }, label: '最早的在前', group: 'time' },
  { sort: { key: 'time', desc: true }, label: '最新的在前', group: 'time' },
  { sort: { key: 'hearts', desc: true }, label: '愛心多的在前', group: 'hearts' },
  { sort: { key: 'hearts', desc: false }, label: '愛心少的在前', group: 'hearts' },
  { sort: { key: 'hot', desc: true }, label: '熱門的在前', hint: '愛心加上底下的回應數', group: 'hot' },
  { sort: { key: 'hot', desc: false }, label: '冷門的在前', hint: '還沒什麼人理的先看', group: 'hot' },
]

export function sameSort(a: ReplySort, b: ReplySort) {
  return a.key === b.key && a.desc === b.desc
}

export function sortLabel(sort: ReplySort) {
  return REPLY_SORT_OPTIONS.find((option) => sameSort(option.sort, sort))?.label ?? REPLY_SORT_OPTIONS[0].label
}

function scoreOf(reply: RantReply, key: ReplySortKey, index: number): number {
  if (key === 'hearts') return reply.likeCount
  if (key === 'hot') return reply.likeCount + (reply.replies?.length ?? 0)
  // 時間：後端給的順序本來就是由早到晚。有 createdAt 就用它，沒有（舊版後端）就照原本的位置
  const at = reply.createdAt ? Date.parse(reply.createdAt) : NaN
  return Number.isNaN(at) ? index : at
}

/**
 * 回傳排好的新陣列，不動原本那個。
 * 分數一樣的維持原本的先後（由早到晚）——不然每次重畫，同分的幾則可能互換位置。
 */
export function sortReplies(replies: RantReply[], sort: ReplySort): RantReply[] {
  if (sameSort(sort, DEFAULT_REPLY_SORT)) return replies
  return replies
    .map((reply, index) => ({ reply, index, score: scoreOf(reply, sort.key, index) }))
    .sort((a, b) => (sort.desc ? b.score - a.score : a.score - b.score) || a.index - b.index)
    .map((entry) => entry.reply)
}

// ── 選到哪一種：整個 App 共用一份，記在這台裝置上 ──
//
// 不是每張卡片各記各的：在這一篇選了「愛心多的在前」，點開下一篇又變回預設，
// 等於每一篇都要重選一次。也不存到後端——這是個人的閱讀習慣，換一台裝置重選一次無妨。

const STORAGE_KEY = 'same-day-rant-reply-sort'
const listeners = new Set<() => void>()
let current: ReplySort = read()

function read(): ReplySort {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? 'null') as Partial<ReplySort> | null
    const found = saved && REPLY_SORT_OPTIONS.find((option) => option.sort.key === saved.key && option.sort.desc === saved.desc)
    return found ? found.sort : DEFAULT_REPLY_SORT
  } catch {
    // 無痕模式、儲存空間被封鎖、存的不是 JSON：都當作沒選過
    return DEFAULT_REPLY_SORT
  }
}

export function setReplySort(next: ReplySort) {
  current = next
  try { localStorage.setItem(STORAGE_KEY, JSON.stringify(next)) } catch { /* 記不住就只在這次有效 */ }
  listeners.forEach((notify) => notify())
}

function subscribe(notify: () => void) {
  listeners.add(notify)
  return () => { listeners.delete(notify) }
}

/** 目前選到的排序。任何一處改了，所有用到的地方一起換。 */
export function useReplySort(): ReplySort {
  return useSyncExternalStore(subscribe, () => current)
}
