namespace SameDaySocialApp.Application.Services;

/// <summary>
/// 同一個信箱多久能再收到一封驗證信。
///
/// 為什麼不是靠 IP 限流就好：AuthController 的 "auth" policy 是每 IP 每分鐘
/// 10 次，換個 IP 就能對同一個信箱繼續寄。對收件人來說那是信箱被灌爆；
/// 對這個服務來說更糟——寄信走的是 Gmail SMTP，有每日上限，額度一旦被耗光，
/// 所有人的註冊信與重設密碼信都寄不出去，整個註冊流程停擺。
///
/// 為什麼要在查帳號之前就擋：重設密碼刻意對「查無此帳號」回覆成功
/// （去帳號枚舉）。如果冷卻只套用在帳號存在的那條路上，攻擊者就有了一個
/// 新的判斷方式——同一個信箱連打兩次，會被擋的就是註冊過的。
/// 所以這一層只看信箱字串，不看帳號存不存在。
///
/// 計數在記憶體：重啟會放過幾封，但這是「煩人」等級的濫用，不值得為它
/// 在每次寄信前多一次資料庫往返。真正的帳號安全靠的是驗證碼本身的
/// 嘗試次數上限（AuthSecurity.MaxCodeAttempts）。
/// </summary>
public sealed class EmailSendThrottle
{
    /// <summary>兩封之間至少隔這麼久。</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    /// <summary>同一個信箱一天最多幾封。</summary>
    public const int MaxPerDay = 10;

    private readonly object gate = new();
    private readonly Dictionary<string, Entry> sends = [];

    private sealed record Entry(DateTimeOffset LastSentAt, DateOnly Day, int SentToday);

    /// <summary>
    /// 佔下一次寄信的額度。還在冷卻中或今天已經寄滿就回 false。
    /// </summary>
    public bool TryStartSend(string email, DateTimeOffset now)
    {
        var key = email.Trim().ToLowerInvariant();
        if (key.Length == 0)
        {
            return false;
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);

        lock (gate)
        {
            if (sends.TryGetValue(key, out var entry) && entry.Day == today)
            {
                if (now - entry.LastSentAt < Cooldown || entry.SentToday >= MaxPerDay)
                {
                    return false;
                }
                sends[key] = new Entry(now, today, entry.SentToday + 1);
                return true;
            }

            sends[key] = new Entry(now, today, 1);
            return true;
        }
    }

    /// <summary>
    /// 寄信失敗時把剛才佔下的額度還回去。沒寄出去的信不該佔用冷卻——
    /// 不然 SMTP 一時不通，使用者就得等滿一分鐘才能再試。
    /// </summary>
    public void ReleaseSend(string email)
    {
        var key = email.Trim().ToLowerInvariant();

        lock (gate)
        {
            if (sends.TryGetValue(key, out var entry))
            {
                sends[key] = entry with
                {
                    LastSentAt = entry.LastSentAt - Cooldown,
                    SentToday = Math.Max(0, entry.SentToday - 1),
                };
            }
        }
    }
}
