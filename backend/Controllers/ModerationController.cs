using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Controllers;

/// <summary>
/// 審核後台。這裡讀得到使用者的貼文與私訊內容，因此：
///  1. 每個動作都先過 AdminAccessService（預設拒絕）
///  2. 回傳欄位刻意最小化，只給審核判斷需要的內容
///  3. 查看與處置都寫入審核軌跡
/// </summary>
[ApiController]
[Route("api/moderation")]
[Authorize]
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
            r.Resolution)).ToList();

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
    string? Resolution);

public sealed record ReviewReportRequest(
    ReportStatus Status,
    string? Resolution = null,
    bool? HideTarget = null);
