namespace SameDaySocialApp.Application.Validation;

/// <summary>
/// 寫入端點的欄位上限。
///
/// 為什麼要有這一層：資料庫裡幾個最容易被塞爆的欄位沒有長度（chat_messages.content
/// 是 text，興趣標籤、照片網址都是 text[]），而全站的請求上限原本是 ASP.NET Core
/// 的預設值約 28.6 MB。一個帳號把自己的 profile 撐到幾十 MB，之後每次配對都要把
/// 整張 users 表載進記憶體；Supabase 免費方案只有 500 MB，少數幾個帳號就能灌滿，
/// 然後所有人的寫入一起失敗。
///
/// 另一半是把 500 變成 400：有長度的欄位（varchar(n)）超長會在 SaveChanges 丟
/// Npgsql 例外，使用者看到的是「伺服器錯誤」而不是「這個欄位太長了」。
///
/// 數字都比現有資料寬鬆很多——線上 81 個帳號最多 5 個標籤、最長 6 個字、
/// 3 張照片、自我介紹 45 字，沒有人會被這些上限擋住。
/// </summary>
public static class InputLimits
{
    public const int MaxTagCount = 10;
    public const int MaxTagLength = 20;
    public const int MaxPhotos = 6;

    /// <summary>聊天訊息。前端輸入框沒有上限，而 chat_messages.content 是沒有長度的 text。</summary>
    public const int MaxMessageLength = 2000;

    /// <summary>引用的那一段。對齊 chat_messages.quoted_content 的 varchar(2000)。</summary>
    public const int MaxQuotedLength = 2000;

    /// <summary>null 當成「這次沒送這個欄位」，一律放行。</summary>
    public static bool WithinLength(string? value, int max)
        => value == null || value.Trim().Length <= max;

    public static bool WithinList(IReadOnlyCollection<string?>? values, int maxCount, int maxLength)
        => values == null || (values.Count <= maxCount && values.All(v => (v?.Trim().Length ?? 0) <= maxLength));
}
