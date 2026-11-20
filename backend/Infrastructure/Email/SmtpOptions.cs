namespace SameDaySocialApp.Infrastructure.Email;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "同頻 Today";

    /// <summary>
    /// 一封信最多等幾秒。
    ///
    /// 沒有這個設定時，整條寄信路徑是沒有上限的：`SmtpClient` 預設 100 秒，
    /// 而後台處理檢舉會循序寄兩封（檢舉人、被檢舉人），所以 Gmail 擋住或網路卡住時，
    /// 管理者按下「處理」之後要轉 200 秒，而且畫面上看不出在等什麼。
    ///
    /// 10 秒是對 Gmail SMTP 的正常值（實測多在 1-3 秒）留了餘裕之後的值。
    /// 寄不出去本來就有處理：註冊／重設密碼回 EMAIL_SEND_FAILED 並退還冷卻，
    /// 檢舉結案把 outcome_mail_sent 記成 false。等久一點不會讓這些路徑變好。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(FromEmail);
}
