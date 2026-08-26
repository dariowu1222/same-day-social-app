using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Domain.Entities;

/// <summary>
/// 使用者檢舉。統一承接樹洞貼文、回應、聊天訊息與使用者的檢舉，
/// 取代原本分散的 rant_reports / chat_reports，讓審核端只需查一張表。
/// </summary>
public sealed class Report
{
    public string Id { get; set; } = "";

    // 檢舉標的：型別 + id 的多型組合
    public ReportTargetType TargetType { get; set; }
    public string TargetId { get; set; } = "";

    public string ReporterId { get; set; } = "";

    /// <summary>被檢舉的人。冗餘存放，供統計累犯用。</summary>
    public string ReportedUserId { get; set; } = "";

    public ReportCategory Category { get; set; }

    /// <summary>檢舉人補充說明（選填）。</summary>
    public string? Detail { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.PENDING;

    /// <summary>由分類推導的嚴重度，供審核佇列排序；數字越大越優先。</summary>
    public int Severity { get; set; }

    /// <summary>
    /// 檢舉當下的內容快照。被檢舉的內容可能隨後被刪除或修改，
    /// 沒有快照就會出現「收到檢舉卻看不到被檢舉什麼」的窘境。
    /// </summary>
    public string? ContextSnapshot { get; set; }

    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>審核結論說明。</summary>
    public string? Resolution { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
