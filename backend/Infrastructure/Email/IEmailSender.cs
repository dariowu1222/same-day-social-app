namespace SameDaySocialApp.Infrastructure.Email;

public interface IEmailSender
{
    Task SendRegistrationVerificationCodeAsync(string toEmail, string code, CancellationToken cancellationToken);
    Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken cancellationToken);

    /// <summary>進入審核後台時的一次性驗證碼。</summary>
    Task SendAdminLoginCodeAsync(string toEmail, string code, CancellationToken cancellationToken);

    /// <summary>後台執行不可逆處置前的再確認驗證碼。信裡必須講明這碼授權什麼。</summary>
    Task SendAdminReverifyCodeAsync(string toEmail, string code, CancellationToken cancellationToken);
    /// <summary>
    /// 檢舉處理完的結果，寄給檢舉的那個人。
    /// upheld 為 true 代表成立。信裡不提被檢舉的是誰——他本來就知道自己檢舉了什麼，
    /// 多寫一個名字只會變成下一次衝突的材料。
    /// </summary>
    Task SendReportOutcomeToReporterAsync(string toEmail, bool upheld, CancellationToken cancellationToken);

    /// <summary>
    /// 檢舉處理完的結果，寄給被檢舉的那個人。
    /// 絕對不提是誰檢舉的——那是報復的起點。
    /// </summary>
    Task SendReportOutcomeToReportedAsync(string toEmail, bool upheld, bool contentHidden, CancellationToken cancellationToken);

}
