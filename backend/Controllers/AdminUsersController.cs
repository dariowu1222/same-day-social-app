using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Controllers;

/// <summary>
/// 後台會員管理。與 ModerationController 共用同一套守門原則：
/// 預設拒絕、未授權回 404（不洩漏端點存在）、每個動作寫入審核軌跡。
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
public sealed class AdminUsersController(
    AdminUserService users,
    ReportService reports,
    AdminAccessService admin) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    private bool Denied() => !admin.IsAdmin(CallerId);

    [HttpGet("dashboard")]
    public ActionResult<ApiResponse<AdminUserService.Dashboard>> Dashboard()
    {
        if (Denied()) return NotFound();
        // 進後台時順手讓到期的停權失效，不必額外排程
        users.RestoreExpiredSuspensions();
        return ApiResponse<AdminUserService.Dashboard>.Ok(users.GetDashboard());
    }

    [HttpGet("users")]
    public ActionResult<ApiResponse<PagedUsers>> Search(
        [FromQuery] string? keyword,
        [FromQuery] AccountStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (Denied()) return NotFound();
        var (items, total) = users.Search(keyword, status, page, pageSize);
        reports.LogAudit(CallerId, "VIEW_USERS", null, $"keyword={keyword}, status={status}, 筆數={items.Count}");
        return ApiResponse<PagedUsers>.Ok(new PagedUsers(items, total, page, pageSize));
    }

    [HttpGet("users/{userId}")]
    public ActionResult<ApiResponse<AdminUserService.UserDetail>> Detail(string userId)
    {
        if (Denied()) return NotFound();
        var detail = users.GetDetail(userId);
        if (detail == null)
        {
            return NotFound(ApiResponse<AdminUserService.UserDetail>.Fail("USER_NOT_FOUND", "找不到這個會員。"));
        }
        reports.LogAudit(CallerId, "VIEW_USER_DETAIL", null, userId);
        return ApiResponse<AdminUserService.UserDetail>.Ok(detail);
    }

    [HttpPut("users/{userId}")]
    public ActionResult<ApiResponse<bool>> Update(string userId, [FromBody] AdminUpdateUserRequest request)
    {
        if (Denied()) return NotFound();
        var ok = users.UpdateProfile(
            userId, CallerId, request.Nickname, request.Bio, request.MembershipTier, request.MembershipExpiresAt);
        return ok
            ? ApiResponse<bool>.Ok(true)
            : NotFound(ApiResponse<bool>.Fail("USER_NOT_FOUND", "找不到這個會員。"));
    }

    /// <summary>
    /// 帳號處置。action: SUSPEND / UNSUSPEND / BAN / UNBAN / DELETE。
    /// 一個端點涵蓋所有處置，前端只送 action，避免後台要記一堆路徑。
    /// </summary>
    [HttpPost("users/{userId}/sanction")]
    public ActionResult<ApiResponse<bool>> Sanction(string userId, [FromBody] SanctionRequest request)
    {
        if (Denied()) return NotFound();

        // 管理者不該把自己鎖在門外
        if (userId == CallerId)
        {
            return BadRequest(ApiResponse<bool>.Fail("SANCTION_SELF", "無法對自己的帳號執行處置。"));
        }

        var ok = request.Action switch
        {
            SanctionAction.SUSPEND => users.Suspend(userId, CallerId, request.Reason, request.ExpiresAt, request.ReportId),
            SanctionAction.UNSUSPEND => users.Unsuspend(userId, CallerId, request.Reason),
            SanctionAction.BAN => users.Ban(userId, CallerId, request.Reason, request.ReportId),
            SanctionAction.UNBAN => users.Unban(userId, CallerId, request.Reason),
            SanctionAction.DELETE => users.MarkDeleted(userId, CallerId, request.Reason, request.ReportId),
            _ => false,
        };

        if (!ok)
        {
            return BadRequest(ApiResponse<bool>.Fail("SANCTION_FAILED", "處置未完成，請確認會員是否存在或動作是否支援。"));
        }
        return ApiResponse<bool>.Ok(true);
    }
}

public sealed record PagedUsers(
    List<AdminUserService.UserSummary> Items, int Total, int Page, int PageSize);

public sealed record AdminUpdateUserRequest(
    string? Nickname = null,
    string? Bio = null,
    MembershipTier? MembershipTier = null,
    DateTimeOffset? MembershipExpiresAt = null);

public sealed record SanctionRequest(
    SanctionAction Action,
    string? Reason = null,
    DateTimeOffset? ExpiresAt = null,
    string? ReportId = null);
