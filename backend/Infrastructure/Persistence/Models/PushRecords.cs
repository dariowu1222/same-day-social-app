namespace SameDaySocialApp.Infrastructure.Persistence.Models;

/// <summary>
/// 一台裝置的 FCM token。唯一鍵是 token 而不是 user_id——
/// 一個人可以有多台裝置，而同一台裝置換帳號登入時要改綁到新的使用者。
/// </summary>
public sealed class DeviceTokenRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Token { get; set; } = "";
    public string Platform { get; set; } = "android";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 每人一列的推播水位。
///
/// LastEventAt 是「上次推播時，那批未讀裡最新一則的時間」：下次執行算出目前的
/// max(created_at)，不大於它就代表沒有新東西，不推。
/// 用事件時間而不是比對未讀數——使用者讀掉 3 則又來 3 則時數字不變，比數字會漏推。
/// </summary>
public sealed class PushDigestStateRecord
{
    public string UserId { get; set; } = "";
    public DateTimeOffset? LastEventAt { get; set; }
    public DateTimeOffset? LastPushedAt { get; set; }
}
