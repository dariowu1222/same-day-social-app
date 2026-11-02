namespace SameDaySocialApp.Infrastructure.Persistence.Models;

/// <summary>
/// 進入後台時寄出的一次性驗證碼。
///
/// 和註冊／重設密碼的驗證碼分開存：那兩種是「證明這個信箱是你的」，
/// 這一種是「證明現在坐在這台裝置前面的是你本人」，用途、存活時間與
/// 失敗處置都不一樣，混在同一張表會讓兩邊的規則互相牽制。
/// </summary>
public sealed class AdminLoginCodeRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string CodeHash { get; set; } = "";

    /// <summary>這組碼能用來做什麼：Login（進後台）或 Reverify（確認不可逆的處置）。
    /// 兩者授權份量差很多，不可互換。</summary>
    public string Purpose { get; set; } = "Login";

    /// <summary>試錯次數。超過上限就作廢，避免六位數被慢慢猜出來。</summary>
    public int Attempts { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 一次後台登入的生命週期。
///
/// 後台 token 不是純 JWT 就算數——它帶的 sid 每次請求都要回來對這張表。
/// 這樣才做得到三件純 JWT 做不到的事：立刻撤銷、閒置逾時、以及
/// 「剛才有沒有驗過」的判斷（破壞性動作要用）。
///
/// 代價是每個後台請求多一次查詢。後台流量極低，換這三件事非常划算。
/// </summary>
public sealed class AdminSessionRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";

    /// <summary>最後一次活動時間。閒置超過上限即失效。</summary>
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>最後一次通過信箱驗證碼的時間。封鎖／註銷會要求這個時間夠新。</summary>
    public DateTimeOffset LastVerifiedAt { get; set; }

    /// <summary>絕對上限。即使一直有在動，到這個時間一律重新驗證。</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>建立當下的來源資訊，純供事後稽核查閱，不拿來當驗證條件。</summary>
    public string? CreatedIp { get; set; }
    public string? CreatedUserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
