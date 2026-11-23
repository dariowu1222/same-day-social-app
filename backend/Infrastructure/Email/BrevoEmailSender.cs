using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SameDaySocialApp.Infrastructure.Email;

/// <summary>
/// 走 Brevo 的交易郵件 API 寄信（HTTPS，443 埠）。信件內容在 EmailSenderBase。
///
/// 為什麼不是 SMTP：Render 免費方案封鎖對外的 25／465／587 埠，而 Gmail 只開
/// 465 與 587，兩邊沒有交集——線上怎麼調設定都寄不出去，TCP 就連不上，症狀是
/// 十幾秒後逾時。HTTPS 不在封鎖範圍內，也不會因為某天平台改政策又斷掉。
///
/// 規格：POST https://api.brevo.com/v3/smtp/email，驗證用 api-key 標頭，
/// 成功回 201 與 messageId，失敗回 400 與 {code, message}。
/// </summary>
public sealed class BrevoEmailSender(
    IOptions<BrevoOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<BrevoEmailSender> logger) : EmailSenderBase
{
    private const string Endpoint = "https://api.brevo.com/v3/smtp/email";
    private readonly BrevoOptions brevo = options.Value;

    protected override async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        if (!brevo.IsConfigured)
        {
            // 這個字串是 AuthService 用來分辨「沒設定」與「寄失敗」的依據，不要改。
            throw new InvalidOperationException("SMTP_NOT_CONFIGURED");
        }

        var payload = new
        {
            sender = new { name = brevo.FromName, email = brevo.FromEmail },
            to = new[] { new { email = toEmail } },
            subject,
            textContent = body,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("api-key", brevo.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex,
                "寄信失敗：連不上 Brevo API（{ExceptionType}），收件人網域 {Domain}",
                ex.GetType().Name, SmtpEmailSender.DomainOf(toEmail));
            throw;
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            // Brevo 的錯誤回應是 {"code": "...", "message": "..."}，兩個欄位都不含機密，
            // 原樣記下來最有診斷價值：unauthorized 是 key 不對，not_enough_credits 是額度用完，
            // 寄件者沒驗證過則會在 message 裡講明。
            var detail = await SafeReadAsync(response, cancellationToken);
            logger.LogError(
                "寄信失敗：Brevo 回 {StatusCode}，內容 {Detail}，收件人網域 {Domain}，主旨「{Subject}」",
                (int)response.StatusCode, detail, SmtpEmailSender.DomainOf(toEmail), subject);

            throw new InvalidOperationException($"BREVO_SEND_FAILED: {(int)response.StatusCode} {detail}");
        }
    }

    /// <summary>讀回應內容失敗不該蓋掉原本的錯誤，所以這裡自己吞掉例外。</summary>
    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            return text.Length > 500 ? text[..500] : text;
        }
        catch
        {
            return "(讀不到回應內容)";
        }
    }
}
