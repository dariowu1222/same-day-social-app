using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using SameDaySocialApp.Infrastructure.Email;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 寄信要有上限，而且上限真的要生效。
///
/// 為什麼需要這組測試：
/// `SmtpClient` 預設等 100 秒，而後台處理檢舉會循序寄兩封（檢舉人、被檢舉人），
/// 所以對方不回應時，管理者按下「處理」之後要轉 200 秒，畫面上看不出在等什麼。
///
/// 為什麼不能只設 `SmtpClient.Timeout`：
/// 那個屬性只對同步的 `Send` 有效，對 `SendMailAsync` 沒有作用。真正夾住非同步
/// 這條路的是傳進去的 CancellationToken——所以這組測試釘的是「**逾時會發生**」，
/// 不是「有設那個屬性」。
///
/// 兩條測試各釘一半，變異測試對過（拿掉哪一半就只有對應的那一條會紅）：
/// 拿掉 `deadline.CancelAfter(timeout)` → 只有第一條紅；
/// 把 `CreateLinkedTokenSource(cancellationToken)` 改成不連動的 `new CancellationTokenSource()`
/// → 只有第二條紅。兩條都留著才蓋得完。
///
/// 這裡用一個「收得下連線但從不回話」的 TCP listener 當對方：那正是最常見也最難查的
/// 那種失敗（連得上、但沒有回應），而不是「連不上」——後者 TCP 層自己有約 20 秒的底，
/// 不歸我們管，程式裡的註解也寫明了這段夾不住。
/// </summary>
public sealed class SmtpTimeoutTests
{
    /// <summary>逾時設 1 秒，給 8 秒的判定窗。差距夠大，不會因為機器忙就偽陽性。</summary>
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(8);

    [Fact]
    public async Task SendingGivesUpWhenTheServerNeverAnswers()
    {
        using var server = new SilentSmtpServer();
        var sender = SenderFor(server.Port, timeoutSeconds: 1);

        var clock = Stopwatch.StartNew();
        var send = Record.ExceptionAsync(() =>
            sender.SendRegistrationVerificationCodeAsync("someone@example.com", "123456", CancellationToken.None));

        var finished = await Task.WhenAny(send, Task.Delay(Window));
        clock.Stop();

        Assert.True(
            finished == send,
            $"寄信沒有在 {Window.TotalSeconds} 秒內放棄——逾時沒有生效，實際等了 {clock.Elapsed.TotalSeconds:F1} 秒以上");

        // 放棄的方式不拘（取消或 SmtpException），但一定要是失敗，
        // 不能靜悄悄地當成寄出去了——上層是靠例外去記 EMAIL_SEND_FAILED 的。
        Assert.NotNull(await send);
    }

    /// <summary>
    /// 呼叫端的 token 先取消時也要停下來。使用者關掉分頁、請求被中止之後，
    /// 這封信不該繼續佔著 SMTP 連線等滿逾時。
    /// </summary>
    [Fact]
    public async Task SendingStopsWhenTheCallerCancels()
    {
        using var server = new SilentSmtpServer();
        // 逾時故意設得很長，確保停下來的原因是呼叫端的 token 而不是逾時
        var sender = SenderFor(server.Port, timeoutSeconds: 600);

        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var send = Record.ExceptionAsync(() =>
            sender.SendRegistrationVerificationCodeAsync("someone@example.com", "123456", caller.Token));

        var finished = await Task.WhenAny(send, Task.Delay(Window));

        Assert.True(finished == send, $"呼叫端取消之後，寄信沒有在 {Window.TotalSeconds} 秒內停下來");
        Assert.NotNull(await send);
    }

    private static SmtpEmailSender SenderFor(int port, int timeoutSeconds)
        => new(Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = port,
            // 這個 listener 不講 TLS，開著的話會先卡在交握上，測不到我們想測的東西
            EnableSsl = false,
            Username = "tester",
            Password = "secret",
            FromEmail = "noreply@example.com",
            TimeoutSeconds = timeoutSeconds
        }));

    /// <summary>
    /// 收得下連線、但一個位元組都不回。SMTP 的客戶端會一直等那句 220 問候語。
    /// </summary>
    private sealed class SilentSmtpServer : IDisposable
    {
        private readonly TcpListener listener;
        private readonly CancellationTokenSource stopping = new();
        private readonly List<TcpClient> accepted = [];

        public SilentSmtpServer()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _ = AcceptLoopAsync();
        }

        public int Port { get; }

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (!stopping.IsCancellationRequested)
                {
                    // 接受連線之後什麼都不做——連線就這樣掛著
                    accepted.Add(await listener.AcceptTcpClientAsync(stopping.Token));
                }
            }
            catch (OperationCanceledException)
            {
                // 收攤
            }
            catch (SocketException)
            {
                // listener 已經停掉
            }
        }

        public void Dispose()
        {
            stopping.Cancel();
            foreach (var client in accepted)
            {
                client.Dispose();
            }
            listener.Stop();
            stopping.Dispose();
        }
    }
}
