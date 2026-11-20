// 「年滿 18 歲」的判斷。Email 註冊、社群登入的確認畫面、既有帳號的補問都用這一個。
//
// 必須和後端 AuthService.IsAdultBirthYear 一致：只取前四個數字、1940 以後、
// 今年減 18 以前。兩邊不同步的話，前端放行的年次會被後端退回，
// 使用者只看得到一句「未滿 18 歲」而不知道自己哪裡填錯。
export function isAdultBirthYear(birthYear: string) {
  const normalized = birthYear.replace(/\D/g, '').slice(0, 4)
  if (normalized.length < 4) return false

  const year = Number(normalized)
  const currentYear = new Date().getFullYear()
  return year >= 1940 && year <= currentYear - 18
}
