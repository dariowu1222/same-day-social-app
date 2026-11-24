// 樹洞的頭像：暱稱首字 + 三種底色輪流。
//
// 以前整面牆的頭像是同一個顏色，一串回應展開之後分不出誰是誰，只能一個一個讀名字。
// 樹洞是匿名的，沒有照片可以放，能幫忙分辨的只剩顏色——所以用暱稱算出一個固定的
// 色調：同一個人不管在哪一則都是同一個顏色，不同人大致會錯開。
//
// 只有三種是故意的。顏色是拿來「分辨相鄰的兩個人」，不是拿來當身分證；
// 種類一多，畫面就花了。

export function avatarLetter(nickname: string): string {
  return nickname.trim()[0]?.toUpperCase() ?? '?'
}

export type AvatarTone = 'a' | 'b' | 'c'

const TONES: AvatarTone[] = ['a', 'b', 'c']

export function avatarTone(nickname: string): AvatarTone {
  let sum = 0
  for (const ch of nickname.trim()) sum = (sum + (ch.codePointAt(0) ?? 0)) % 9973
  return TONES[sum % TONES.length]
}
