namespace SameDaySocialApp.Infrastructure.Email;

/// <summary>
/// Brevo（前 Sendinblue）的交易郵件 API 設定。
///
/// 為什麼需要它：Render 免費方案封鎖對外的 SMTP 埠，Gmail 只開 465／587，
/// 兩邊沒有交集，所以線上寄信一定得改走 HTTPS。Brevo 的 API 走 443，不受影響。
///
/// FromEmail 必須先在 Brevo 後台驗證過，否則 API 會回 400。
/// </summary>
public sealed class BrevoOptions
{
    /// <summary>Brevo 後台產生的 API key（機密，只放環境變數／User Secrets）。</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>寄件信箱。必須是 Brevo 後台已驗證的寄件者。</summary>
    public string FromEmail { get; set; } = "";

    public string FromName { get; set; } = "同頻 Today";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(FromEmail);
}
