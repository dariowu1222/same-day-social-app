// Profile 功能共用的純函式與常數（生日 / 星座 / 年齡 / 照片壓縮）。

export const ZODIAC_ICON: Record<string, string> = {
  '牡羊座': '♈', '金牛座': '♉', '雙子座': '♊', '巨蟹座': '♋',
  '獅子座': '♌', '處女座': '♍', '天秤座': '♎', '天蠍座': '♏',
  '射手座': '♐', '摩羯座': '♑', '水瓶座': '♒', '雙魚座': '♓',
}

export const MONTH_NAMES = ['1月','2月','3月','4月','5月','6月','7月','8月','9月','10月','11月','12月']
export const DOW_LABELS = ['日','一','二','三','四','五','六']
export const DEFAULT_BIRTHDAY_YEAR = 2000
export const MINIMUM_AGE = 18

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

export function isAdultBirthday(birthday: string): boolean {
  return getAge(birthday) >= MINIMUM_AGE
}

export async function fileToResizedBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const img = new Image()
    const url = URL.createObjectURL(file)
    img.onload = () => {
      URL.revokeObjectURL(url)
      const maxSize = 600
      const scale = Math.min(1, maxSize / Math.max(img.width, img.height))
      const w = Math.round(img.width * scale)
      const h = Math.round(img.height * scale)
      const canvas = document.createElement('canvas')
      canvas.width = w
      canvas.height = h
      canvas.getContext('2d')!.drawImage(img, 0, 0, w, h)
      resolve(canvas.toDataURL('image/jpeg', 0.82))
    }
    img.onerror = reject
    img.src = url
  })
}
