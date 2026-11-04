using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Infrastructure.Security;

namespace SameDaySocialApp.Controllers;

/// <summary>
/// 後台的 AI 用量與花費。與 AdminUsersController 共用同一套守門原則：
/// 預設拒絕、未授權回 404（不洩漏端點存在）、查詢也寫入審核軌跡。
///
/// 路由必須掛在 api/admin 底下：Program.cs 有一段中介軟體擋「後台 token 只能打
/// 後台路徑」，取別的前綴會被回 401 ADMIN_TOKEN_SCOPE，而前端看起來像登入過期。
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
[ServiceFilter(typeof(AdminSessionFilter))]
public sealed class AdminAiUsageController(
    AiUsageService usage,
    ReportService reports,
    AdminAccessService admin) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    // filter 已經擋過一次，這裡再擋一次是刻意的：守門邏輯不該只有一個地方知道，
    // 哪天 filter 被誰從類別上拿掉，端點也不會就這樣敞開。
    private bool Denied() => !admin.IsAdmin(CallerId);

    [HttpGet("ai-usage")]
    public ActionResult<ApiResponse<AiUsageService.Summary>> Summary()
    {
        if (Denied()) return NotFound();
        var summary = usage.GetSummary();
        reports.LogAudit(CallerId, "VIEW_AI_USAGE", null,
            $"本月 {summary.ThisMonth.Calls} 次 / US${summary.ThisMonth.CostUsd:F4}");
        return ApiResponse<AiUsageService.Summary>.Ok(summary);
    }

    [HttpGet("ai-usage/logs")]
    public ActionResult<ApiResponse<PagedAiUsage>> Logs(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (Denied()) return NotFound();

        // 查無資料要回 Ok(空集合)，不能回 NotFound——後台前端把 /api/admin 的 404
        // 一律當成「這顆 token 沒用」，會把人踢回登入頁。
        var (items, total) = usage.Search(status, page, pageSize);
        return ApiResponse<PagedAiUsage>.Ok(new PagedAiUsage(items, total, page, pageSize));
    }
}

public sealed record PagedAiUsage(
    List<AiUsageService.UsageEntry> Items, int Total, int Page, int PageSize);
