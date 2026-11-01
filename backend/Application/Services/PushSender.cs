using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SameDaySocialApp.Application.Services;

/// <summary>
/// 推播送信設定。
///
/// 憑證走 base64 環境變數而不是檔案：Render 不給持久化檔案系統，
/// 而 service account JSON 的 private_key 裡有跳脫換行，原文貼進
/// Render 的輸入框很容易被改壞；base64 是一整行純 ASCII，貼不壞。
/// </summary>
public sealed class PushOptions
{
    public string? FirebaseServiceAccountJsonBase64 { get; set; }

    /// <summary>排程觸發端點的共享密鑰。沒設就等於整個推播功能關閉。</summary>
    public string? TriggerSecret { get; set; }

    /// <summary>單次執行最多處理幾人。跑不完的下一輪（兩小時後）補上。</summary>
    public int MaxUsersPerRun { get; set; } = 500;

    /// <summary>未讀只往回看這麼多天，避免掃全表。</summary>
    public int LookbackDays { get; set; } = 14;

    /// <summary>距離上次推播至少要隔這麼久。排程早到幾分鐘時不會整次被吃掉。</summary>
    public int MinIntervalMinutes { get; set; } = 110;

    /// <summary>安靜時段起（台北時間，含）。</summary>
    public int QuietStartHour { get; set; } = 23;

    /// <summary>安靜時段迄（台北時間，不含）。</summary>
    public int QuietEndHour { get; set; } = 8;
}

/// <summary>一次送信的結果。成功數與失效 token 分開回報——呼叫端要靠成功數決定水位推不推。</summary>
public sealed record PushSendResult(int SuccessCount, List<string> DeadTokens);

/// <summary>
/// FCM HTTP v1 送信。
///
/// 用 FirebaseAdmin 而不是自己打 REST，關鍵不是省掉換 OAuth2 token 的程式碼，
/// 而是 SendEachForMulticastAsync 的回應會逐 token 告訴你哪些是 Unregistered /
/// InvalidArgument——那是清理 device_tokens 唯一可靠的來源。
/// （legacy server key 已被 Google 停用，沒有別的選擇。）
/// </summary>
public sealed class PushSender
{
    private readonly PushOptions options;
    private readonly ILogger<PushSender> logger;
    private readonly Lazy<FirebaseMessaging?> messaging;

    public PushSender(IOptions<PushOptions> options, ILogger<PushSender> logger)
    {
        this.options = options.Value;
        this.logger = logger;
        messaging = new Lazy<FirebaseMessaging?>(CreateMessaging, isThreadSafe: true);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.FirebaseServiceAccountJsonBase64);

    private FirebaseMessaging? CreateMessaging()
    {
        if (!IsConfigured) return null;

        try
        {
            var json = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(options.FirebaseServiceAccountJsonBase64!.Trim()));

            var credential = CredentialFactory
                .FromJson<ServiceAccountCredential>(json)
                .ToGoogleCredential();

            // FirebaseApp 是行程層級的單例，重複建立會丟例外。
            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = credential,
            });

            return FirebaseMessaging.GetMessaging(app);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Firebase 憑證無法解析，推播停用");
            return null;
        }
    }

    /// <summary>
    /// 對一批 token 送同一則通知。
    ///
    /// 整批失敗時回傳 SuccessCount = 0，呼叫端據此決定不要推進水位——
    /// 先前的寫法是把例外吞掉、照樣回報成功，結果 FCM 一故障就靜默吃掉
    /// 所有人的那批未讀，而且再也不會補推。
    /// </summary>
    public async Task<PushSendResult> SendAsync(
        List<string> tokens, string title, string body, string? roomId, CancellationToken ct)
    {
        var client = messaging.Value;
        if (client == null || tokens.Count == 0)
        {
            return new PushSendResult(0, []);
        }

        var data = new Dictionary<string, string> { ["type"] = "digest" };
        if (!string.IsNullOrWhiteSpace(roomId)) data["roomId"] = roomId;

        // Tokens 被標記為過時、文件要你改用 Fids，但 FID 是 Firebase Installation ID，
        // 跟 @capacitor/push-notifications 回傳的 registration token 是兩種不同的東西。
        // 照著換會讓推播根本送不到，所以這裡維持 Tokens。
#pragma warning disable CS0618
        var message = new MulticastMessage
        {
            Tokens = tokens,
            Notification = new Notification { Title = title, Body = body },
            Data = data,
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                // 彙總訊息兩小時後就過時了，與其遲到不如不送
                TimeToLive = TimeSpan.FromHours(2),
                Notification = new AndroidNotification { ChannelId = "default", Tag = "tongpin-digest" },
            },
            Apns = new ApnsConfig
            {
                Headers = new Dictionary<string, string>
                {
                    ["apns-expiration"] = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds().ToString(),
                },
                Aps = new Aps { Sound = "default", ThreadId = "tongpin-digest" },
            },
        };
#pragma warning restore CS0618

        try
        {
            var response = await client.SendEachForMulticastAsync(message, ct);
            var dead = new List<string>();

            for (var i = 0; i < response.Responses.Count; i++)
            {
                var item = response.Responses[i];
                if (item.IsSuccess) continue;

                // 只有這兩種代表 token 真的沒救了。Internal / Unavailable 之類是
                // 暫時性的，清掉會讓使用者永久收不到推播。
                var code = item.Exception?.MessagingErrorCode;
                if (code is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                {
                    dead.Add(tokens[i]);
                }
            }

            return new PushSendResult(response.SuccessCount, dead);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FCM 送信失敗，這批 {Count} 個 token 全部未送達", tokens.Count);
            return new PushSendResult(0, []);
        }
    }
}
