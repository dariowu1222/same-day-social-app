// 使用者顯示相關的共用小工具：年齡、星座、頭像 fallback。

export const ZODIAC_ICON: Record<string, string> = {
  '牡羊座': '♈', '金牛座': '♉', '雙子座': '♊', '巨蟹座': '♋',
  '獅子座': '♌', '處女座': '♍', '天秤座': '♎', '天蠍座': '♏',
  '射手座': '♐', '摩羯座': '♑', '水瓶座': '♒', '雙魚座': '♓',
}

export function getZodiac(birthday: string): string {
  const d = new Date(birthday)
  const md = (d.getMonth() + 1) * 100 + d.getDate()
  if (md >= 120 && md <= 218) return '水瓶座'
  if (md >= 219 && md <= 320) return '雙魚座'
  if (md >= 321 && md <= 419) return '牡羊座'
  if (md >= 420 && md <= 520) return '金牛座'
  if (md >= 521 && md <= 620) return '雙子座'
  if (md >= 621 && md <= 722) return '巨蟹座'
  if (md >= 723 && md <= 822) return '獅子座'
  if (md >= 823 && md <= 922) return '處女座'
  if (md >= 923 && md <= 1022) return '天秤座'
  if (md >= 1023 && md <= 1121) return '天蠍座'
  if (md >= 1122 && md <= 1221) return '射手座'
  return '摩羯座'
}

export function getAge(birthday: string): number {
  const today = new Date()
  const [year, month, day] = birthday.split('-').map(Number)
  let age = today.getFullYear() - year
  const m = today.getMonth() + 1 - month
  if (m < 0 || (m === 0 && today.getDate() < day)) age--
  return age
}

// 照片失效／未設定時的防呆頭像：依使用者 ID 穩定挑一隻動物。
const FALLBACK_ANIMALS = ['🦊', '🐻', '🐰', '🐱', '🐶', '🐼', '🐨', '🐸', '🦝', '🐯', '🦁', '🐹', '🐧', '🦉', '🐢', '🦔']

export function animalFor(seed: string): string {
  let hash = 0
  for (let i = 0; i < seed.length; i++) {
    hash = (hash * 31 + seed.charCodeAt(i)) | 0
  }
  return FALLBACK_ANIMALS[Math.abs(hash) % FALLBACK_ANIMALS.length]
}

// ── 測試用假頭像：正式環境會改用帳號實際上傳的 photoDataUrls[0]（之後刪除）──
export function testAvatarPhoto(seed: string): string {
  return `https://picsum.photos/seed/chat_${encodeURIComponent(seed)}/200/200`
}
