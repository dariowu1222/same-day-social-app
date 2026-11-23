using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SameDaySocialApp.Infrastructure.Email;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 走 HTTP API 寄信的那條路。
///
/// 為什麼要測一個「只是送個 HTTP 請求」的類別：這裡每一個欄位名都是跟外部服務
/// 約定好的，拼錯一個字（textContent 寫成 textPlain、api-key 寫成 apiKey）編譯器
/// 不會說話，測試不寫就只能等上線後使用者收不到信才發現——而那正是這次花了一整天
/// 追的那個問題。
/// </summary>
public sealed class BrevoEmailSenderTests
{
    private const string ToEmail = "someone@example.com";
    private const string Code = "123456";

    [Fact]
    public async Task 驗證碼信會以_Brevo_約定的格式送出()
    {
        var (sender, handler) = Build();

        await sender.SendRegistrationVerificationCodeAsync(ToEmail, Code, CancellationToken.None);

        Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.LastUrl);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);

        var json = JsonDocument.Parse(handler.LastBody!).RootElement;
        Assert.Equal("寄件者@example.com", json.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("同頻 Today", json.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal(ToEmail, json.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("同頻 Today 建立帳號驗證碼", json.GetProperty("subject").GetString());

        // 欄位名必須是 textContent，Brevo 不認 textPlain
        Assert.True(json.TryGetProperty("textContent", out var text), "送出的 JSON 沒有 textContent 欄位");
        Assert.Contains(Code, text.GetString());
    }

    /// <summary>API key 要放在 api-key 標頭，不是 Authorization。</summary>
    [Fact]
    public async Task API_key_放在正確的標頭()
    {
        var (sender, handler) = Build();

        await sender.SendRegistrationVerificationCodeAsync(ToEmail, Code, CancellationToken.None);

        Assert.True(handler.LastHeaders!.TryGetValues("api-key", out var values), "沒有帶 api-key 標頭");
        Assert.Equal("test-key", Assert.Single(values));
    }

    /// <summary>
    /// 沒設定時必須丟出 SMTP_NOT_CONFIGURED 這個字串——AuthService 靠它分辨
    /// 「沒設定寄信服務」與「寄失敗」，換個字訊息就會變成錯的那一種。
    /// </summary>
    [Fact]
    public async Task 沒設定時丟出_AuthService_認得的訊息()
    {
        var (sender, _) = Build(apiKey: "");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendRegistrationVerificationCodeAsync(ToEmail, Code, CancellationToken.None));

        Assert.Equal("SMTP_NOT_CONFIGURED", ex.Message);
    }

    /// <summary>
    /// Brevo 回 4xx 一定要丟例外。吞掉的話 AuthService 會以為信寄出去了，
    /// 使用者就會卡在一個永遠等不到驗證碼的畫面。
    /// </summary>
    [Fact]
    public async Task 服務回錯誤時要丟出例外()
    {
        var (sender, _) = Build(
            status: HttpStatusCode.BadRequest,
            responseBody: """{"code":"unauthorized","message":"Key not found"}""");

        await Assert.ThrowsAnyAsync<Exception>(
            () => sender.SendRegistrationVerificationCodeAsync(ToEmail, Code, CancellationToken.None));
    }

    [Fact]
    public async Task 服務回_201_視為成功()
    {
        var (sender, _) = Build(
            status: HttpStatusCode.Created,
            responseBody: """{"messageId":"<abc@relay.brevo.com>"}""");

        // 不丟例外就是通過
        await sender.SendRegistrationVerificationCodeAsync(ToEmail, Code, CancellationToken.None);
    }

    /// <summary>六封信共用同一份文案，隨手抽查幾封確認主旨沒有串錯。</summary>
    [Theory]
    [InlineData("register", "同頻 Today 建立帳號驗證碼")]
    [InlineData("reset", "同頻 Today 密碼重設驗證碼")]
    [InlineData("adminLogin", "同頻 Today 後台登入驗證碼")]
    public async Task 每封信的主旨都對得上(string which, string expectedSubject)
    {
        var (sender, handler) = Build();

        await (which switch
        {
            "register" => sender.SendRegistrationVerificationCodeAsync(ToEmail, Code, CancellationToken.None),
            "reset" => sender.SendPasswordResetCodeAsync(ToEmail, Code, CancellationToken.None),
            _ => sender.SendAdminLoginCodeAsync(ToEmail, Code, CancellationToken.None),
        });

        var json = JsonDocument.Parse(handler.LastBody!).RootElement;
        Assert.Equal(expectedSubject, json.GetProperty("subject").GetString());
        Assert.Contains(Code, json.GetProperty("textContent").GetString());
    }

    private static (BrevoEmailSender Sender, RecordingHandler Handler) Build(
        string apiKey = "test-key",
        HttpStatusCode status = HttpStatusCode.Created,
        string responseBody = """{"messageId":"<x@relay>"}""")
    {
        var handler = new RecordingHandler(status, responseBody);
        var options = Options.Create(new BrevoOptions
        {
            ApiKey = apiKey,
            FromEmail = "寄件者@example.com",
            FromName = "同頻 Today",
        });
        var sender = new BrevoEmailSender(options, new StubFactory(handler), NullLogger<BrevoEmailSender>.Instance);
        return (sender, handler);
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    /// <summary>把送出去的請求留下來給測試檢查，不會真的連出去。</summary>
    private sealed class RecordingHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public string? LastUrl { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastBody { get; private set; }
        public System.Net.Http.Headers.HttpRequestHeaders? LastHeaders { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri?.ToString();
            LastMethod = request.Method;
            LastHeaders = request.Headers;
            LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
