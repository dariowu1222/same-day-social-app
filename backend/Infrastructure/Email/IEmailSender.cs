namespace SameDaySocialApp.Infrastructure.Email;

public interface IEmailSender
{
    Task SendRegistrationVerificationCodeAsync(string toEmail, string code, CancellationToken cancellationToken);
    Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken cancellationToken);

    /// <summary>進入審核後台時的一次性驗證碼。</summary>
    Task SendAdminLoginCodeAsync(string toEmail, string code, CancellationToken cancellationToken);

    /// <summary>後台執行不可逆處置前的再確認驗證碼。信裡必須講明這碼授權什麼。</summary>
    Task SendAdminReverifyCodeAsync(string toEmail, string code, CancellationToken cancellationToken);
}
