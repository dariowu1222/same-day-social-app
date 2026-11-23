using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SameDaySocialApp.Infrastructure.Email;

/// <summary>
/// 走 SMTP 寄信。信件內容在 EmailSenderBase，這裡只管傳輸。
///
/// 注意：Render 的免費方案封鎖對外的 25／465／587 埠，所以這條路在線上是不通的
/// （連 TCP 都連不出去，會卡到逾時）。線上請改用 BrevoEmailSender；這個實作留給
/// 本機開發，本機沒有那層封鎖，用 Gmail 應用程式密碼最省事。
/// 選哪一個由 Program.cs 依設定決定。
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : EmailSenderBase
{
    private readonly SmtpOptions smtp = options.Value;

    protected override async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        if (!smtp.IsConfigured)
        {
            throw new InvalidOperationException("SMTP_NOT_CONFIGURED");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(smtp.FromEmail, smtp.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(toEmail));

        // 下限 1 秒：設定值寫成 0 或負數時不要變成「立刻取消、永遠寄不出去」。
        var timeout = TimeSpan.FromSeconds(Math.Max(1, smtp.TimeoutSeconds));

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            Credentials = new NetworkCredential(smtp.Username, smtp.Password),

            // SmtpClient.Timeout 只對同步的 Send 有效，對 SendMailAsync 沒有作用。
            // 這一行設了也夾不住下面那個 await——留著是為了「逾時只有一個設定來源」，
            // 以免日後有人改走同步路徑時又踩一次沒有上限的坑。
            Timeout = (int)timeout.TotalMilliseconds
        };

        // 真正把非同步這條路夾住的是這裡：SendMailAsync 的 CancellationToken 多載
        // 在 token 觸發時會去呼叫 SendAsyncCancel。要和呼叫端的 token 連動：使用者
        // 關掉分頁、請求被中止時這封信也該停下來，不要佔著 SMTP 連線。
        //
        // 夾不住的那一段：主機不通時 TCP 連線本身有自己的底（實測約 20 秒），
        // 那一層沒有可設定的逾時。這裡擋的是「連得上但對方不回」的情況。
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        try
        {
            await client.SendMailAsync(message, deadline.Token);
        }
        catch (SmtpException ex)
        {
            // StatusCode 是這裡最有價值的一個值：535 是帳密被拒（應用程式密碼失效或被撤銷），
            // 421/450 多半是額度或速率，ServiceNotAvailable 常見於連不上或被擋在 port 之外。
            logger.LogError(ex,
                "寄信失敗：{Host}:{Port} 回 StatusCode={StatusCode}，收件人網域 {Domain}，主旨「{Subject}」",
                smtp.Host, smtp.Port, ex.StatusCode, DomainOf(toEmail), subject);
            throw;
        }
        // 逾時會走到這裡（deadline 觸發、呼叫端 token 未取消）；真正的呼叫端取消
        // （使用者中止請求）原樣往外傳、不記成錯誤，與 BrevoEmailSender 一致。
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex,
                "寄信失敗：連線 {Host}:{Port} 時發生 {ExceptionType}，收件人網域 {Domain}",
                smtp.Host, smtp.Port, ex.GetType().Name, DomainOf(toEmail));
            throw;
        }
    }

    /// <summary>
    /// 只取網域寫進日誌。要判斷的是「我們這端寄不寄得出去」，不是誰收信，
    /// 完整信箱寫進日誌等於把使用者的 Email 複製一份到另一個地方。
    /// </summary>
    internal static string DomainOf(string email)
    {
        var at = email.LastIndexOf('@');
        return at >= 0 && at < email.Length - 1 ? email[(at + 1)..] : "(無法解析)";
    }
}
