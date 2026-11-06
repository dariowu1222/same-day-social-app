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

    public async Task SendReportOutcomeToReporterAsync(string toEmail, bool upheld, CancellationToken cancellationToken)
    {
        await SendCodeAsync(
            toEmail,
            upheld ? "同頻 Today：你的檢舉已處理" : "同頻 Today：你的檢舉處理結果",
            upheld
                ? """
                  你好，

                  你檢舉的內容我們看過了，判定違反社群規範，已經做出處置。
                  謝謝你願意花時間讓這裡好一點。

                  這封信不會透露對方是誰，對方也不會知道是誰檢舉的。
                  """
                : """
                  你好，

                  你檢舉的內容我們看過了，這一次判定沒有違反社群規範，所以不會有處置。

                  這不代表你的感受不重要。如果你覺得判斷有誤，或情況又有新的變化，
                  可以再檢舉一次並在補充說明裡寫下細節，我們會重新看過。
                  如果是讓你不舒服的人，也可以直接在聊天室裡封鎖對方。
                  """,
            cancellationToken);
    }

    public async Task SendReportOutcomeToReportedAsync(string toEmail, bool upheld, bool contentHidden, CancellationToken cancellationToken)
    {
        await SendCodeAsync(
            toEmail,
            upheld ? "同頻 Today：你的一則內容已被處置" : "同頻 Today：關於一則對你的檢舉",
            upheld
                ? $"""
                  你好，

                  你在同頻 Today 的一則內容被檢舉，我們看過之後判定違反社群規範。
                  {(contentHidden ? "該則內容已經隱藏，其他人看不到了。" : "我們已經做出對應的處置。")}

                  如果你認為這是誤判，可以回信說明，我們會再看一次。
                  """
                : """
                  你好，

                  你在同頻 Today 的一則內容被檢舉，我們看過之後判定沒有問題。
                  內容維持原狀，不會有任何處置，你的帳號也不受影響。

                  會通知你，是因為我們不希望你哪天發現東西不見了卻不知道發生過什麼事。
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
