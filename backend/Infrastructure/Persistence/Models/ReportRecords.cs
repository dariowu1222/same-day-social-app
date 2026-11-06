namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class ReportRecord
{
    public string Id { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string ReporterId { get; set; } = "";
    public string ReportedUserId { get; set; } = "";
    public string Category { get; set; } = "";
    public string? Detail { get; set; }
    public string Status { get; set; } = "PENDING";
    public int Severity { get; set; }
    public string? ContextSnapshot { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? Resolution { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 處理結果有沒有寄信成功。null = 沒寄（SELF_HARM，或兩邊都查不到信箱）、
    /// true = 該寄的都寄出去了、false = 有人沒寄成功。
    /// 寄信是會失敗的，而失敗之後沒有任何地方看得出來，管理者會以為當事人
    /// 已經被通知了——所以這一欄要落地，不是只寫 log。
    /// </summary>
    public bool? OutcomeMailSent { get; set; }
}

/// <summary>
/// 審核操作軌跡。後台能讀到使用者的私訊與貼文內容，
/// 因此每一次查看與處置都要留下紀錄，事後可稽核。
/// </summary>
public sealed class ModerationAuditRecord
{
    public string Id { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? ReportId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 帳號處置歷史。每次停權／解除／封鎖／註銷都留一筆，
/// 讓爭議發生時查得到「誰、什麼時候、為什麼」做了處置。
/// 目前生效中的狀態看 users.status，這裡是完整歷程。
/// </summary>
public sealed class AccountSanctionRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Reason { get; set; }
    /// <summary>停權到期時間；null 表示無期限（封鎖）或不適用。</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    public string ActorId { get; set; } = "";
    /// <summary>關聯的檢舉單，供追溯處置來源。</summary>
    public string? ReportId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
