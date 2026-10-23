// API 錯誤碼 → 使用者文案的單一對照表。
// 後端回傳的 code 在這裡集中翻譯，新增錯誤碼只改這一處。
export function toUserMessage(code?: string, message?: string) {
  switch (code) {
    case 'ACCOUNT_NOT_FOUND':
      return '查無此帳號，請確認 Email 是否輸入正確，或先建立帳號。'
    // 密碼規則由後端 ValidatePassword 判定，訊息會隨規則調整，因此優先採用後端文案。
    // 先前這裡寫死「密碼錯誤，請重新輸入。」——那是登入的語氣，但這個碼只出現在
    // 註冊／重設密碼，使用者看到只會以為自己打錯字，不知道是長度不足。
    case 'INVALID_PASSWORD':
      return message ?? '密碼需為 8 到 64 碼。'
    case 'WEAK_PASSWORD':
      return message ?? '密碼需同時包含英文與數字。'
    case 'PASSWORD_NOT_MATCH':
      return message ?? '兩次輸入的密碼不一致。'
    case 'INVALID_LOGIN':
      return '請輸入正確的 Email 與密碼。'
    case 'DATABASE_NOT_CONFIGURED':
      return '目前尚未連接資料庫，無法使用正式登入。'
    case 'DATABASE_UNAVAILABLE':
      return '目前無法連線資料庫，請稍後再試。'
    case 'SMTP_NOT_CONFIGURED':
      return '目前尚未設定寄信服務，無法寄出驗證碼。'
    case 'TERMS_NOT_ACCEPTED':
      return '請先閱讀並同意服務條款與隱私權政策。'
    case 'UNDERAGE':
      return '未滿 18 歲暫時不能使用同頻 Today。'
    case 'INVALID_CODE':
      return '驗證碼錯誤或已過期，請確認後再輸入。'
    case 'EMAIL_SEND_FAILED':
      return '驗證碼寄送失敗，請稍後再試。'
    case 'ACCOUNT_EXISTS':
      return '這個 Email 已經註冊過，請直接登入或使用忘記密碼。'
    default:
      return message ?? '操作失敗，請稍後再試。'
  }
}
