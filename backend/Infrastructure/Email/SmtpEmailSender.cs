using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace SameDaySocialApp.Infrastructure.Email;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions smtp = options.Value;

    public async Task SendRegistrationVerificationCodeAsync(string toEmail, string code, CancellationToken cancellationToken)
    {
        await SendCodeAsync(
            toEmail,
            "同頻 Today 建立帳號驗證碼",
            $"""
            你好，

            你的同頻 Today 建立帳號驗證碼是：{code}

            驗證碼 10 分鐘內有效。若這不是你本人操作，請忽略這封信。
            """,
            cancellationToken);
    }

    public async Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken cancellationToken)
    {
        await SendCodeAsync(
            toEmail,
            "同頻 Today 密碼重設驗證碼",
            $"""
            你好，

            你的同頻 Today 密碼重設驗證碼是：{code}

            驗證碼 10 分鐘內有效。若這不是你本人操作，請忽略這封信。
            """,
            cancellationToken);
    }

    public async Task SendAdminLoginCodeAsync(string toEmail, string code, CancellationToken cancellationToken)
    {
        await SendCodeAsync(
            toEmail,
            "同頻 Today 後台登入驗證碼",
            $"""
            你好，

            有人正在用你的帳號進入同頻 Today 的審核後台，驗證碼是：{code}

            驗證碼 5 分鐘內有效。
            如果這不是你本人操作，代表你的密碼可能已經外洩——請立刻更改密碼。
            """,
            cancellationToken);
    }

    public async Task SendAdminReverifyCodeAsync(string toEmail, string code, CancellationToken cancellationToken)
    {
        await SendCodeAsync(
            toEmail,
            "同頻 Today 後台：確認不可逆的處置",
            $"""
            你好，

            後台正在執行一個無法復原的動作（封鎖或註銷某個帳號），確認碼是：{code}

            這組碼只能用來確認這類處置，不能用來登入。
            驗證碼 5 分鐘內有效。
            如果這不是你本人操作，請立刻更改密碼，並檢查後台的處置紀錄。
            """,
            cancellationToken);
    }

    private async Task SendCodeAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
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

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            Credentials = new NetworkCredential(smtp.Username, smtp.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
