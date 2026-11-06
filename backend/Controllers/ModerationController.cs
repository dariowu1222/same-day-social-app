using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Security;

namespace SameDaySocialApp.Controllers;

/// <summary>
/// 審核後台。這裡讀得到使用者的貼文與私訊內容，因此：
///  1. 只接受後台專用 token（AdminSessionFilter）——一般 App token 一律 404
///  2. 每個動作仍再過一次 AdminAccessService（預設拒絕），不靠單一道防線
///  3. 回傳欄位刻意最小化，只給審核判斷需要的內容
///  4. 查看與處置都寫入審核軌跡
/// </summary>
[ApiController]
[Route("api/moderation")]
[Authorize]
[ServiceFilter(typeof(AdminSessionFilter))]
public sealed class ModerationController(ReportService reports, AdminAccessService admin) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    /// <summary>統一守門。回 404 而非 403，不對外洩漏這組端點的存在。</summary>
    private bool Denied() => !admin.IsAdmin(CallerId);

    [HttpGet("reports")]
    public ActionResult<ApiResponse<List<ModerationReportDto>>> Queue(
        [FromQuery] ReportStatus? status = ReportStatus.PENDING,
        [FromQuery] int limit = 50)
    {
        if (Denied()) return NotFound();

        var queue = reports.GetQueue(status, limit);

        // 這批檢舉指向的貼文現在是不是隱藏中。審核的人要看得到目前狀態，
        // 才知道該按「隱藏」還是「退回」——以前這張清單完全沒有這個資訊。
        var hidden = reports.HiddenRantPosts(
            queue.Where(r => r.TargetType == ReportTargetType.RANT_POST).Select(r => r.TargetId));

        var dto = queue.Select(r => new ModerationReportDto(
            r.Id,
            r.TargetType.ToString(),
            r.TargetId,
            r.Category.ToString(),
            r.Severity,
            r.Status.ToString(),
            r.Detail,
            r.ContextSnapshot,
            r.ReportedUserId,
            reports.CountAgainstUser(r.ReportedUserId),
            r.CreatedAt,
            r.ReviewedBy,
            r.ReviewedAt,
            r.Resolution,
            r.TargetType == ReportTargetType.RANT_POST ? hidden.GetValueOrDefault(r.TargetId) : null)).ToList();

        reports.LogAudit(CallerId, "VIEW_QUEUE", null, $"status={status}, 筆數={dto.Count}");
        return ApiResponse<List<ModerationReportDto>>.Ok(dto);
    }

    [HttpPost("reports/{reportId}/review")]
    public ActionResult<ApiResponse<bool>> Review(string reportId, [FromBody] ReviewReportRequest request)
    {
        if (Denied()) return NotFound();

        var updated = reports.Review(reportId, CallerId, request.Status, request.Resolution, request.HideTarget);
        return updated == null
            ? NotFound(ApiResponse<bool>.Fail("REPORT_NOT_FOUND", "找不到這筆檢舉。"))
            : ApiResponse<bool>.Ok(true);
    }
}

/// <summary>
/// 審核用的檢舉檢視。刻意不含檢舉人身分：
/// 審核不需要知道是誰檢舉的，少一個欄位就少一分外洩與報復風險。
/// </summary>
public sealed record ModerationReportDto(
    string Id,
    string TargetType,
    string TargetId,
    string Category,
    int Severity,
    string Status,
    string? Detail,
    string? ContextSnapshot,
    string ReportedUserId,
    int ReportsAgainstUser,
    DateTimeOffset CreatedAt,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? Resolution,
    /// <summary>
    /// 這篇現在是不是隱藏中。只有 RANT_POST 有值，其他型別為 null（還不支援隱藏）。
    /// 後台靠它決定要顯示「隱藏內容」還是「退回給作者」。
    /// </summary>
    bool? TargetHidden);

public sealed record ReviewReportRequest(
    ReportStatus Status,
    string? Resolution = null,
    bool? HideTarget = null);
