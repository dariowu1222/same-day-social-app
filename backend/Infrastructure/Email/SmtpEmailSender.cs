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
