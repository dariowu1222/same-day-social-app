using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Security;

namespace SameDaySocialApp.Controllers;

/// <summary>
/// 後台會員管理。與 ModerationController 共用同一套守門原則：
/// 預設拒絕、未授權回 404（不洩漏端點存在）、每個動作寫入審核軌跡。
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
[ServiceFilter(typeof(AdminSessionFilter))]
public sealed class AdminUsersController(
    AdminUserService users,
    ReportService reports,
    AdminAccessService admin) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    // filter 已經擋過一次，這裡再擋一次是刻意的：守門邏輯不該只有一個地方知道，
    // 哪天 filter 被誰從類別上拿掉，端點也不會就這樣敞開。
    private bool Denied() => !admin.IsAdmin(CallerId);

    /// <summary>這次請求的驗證碼是否夠新。封鎖／註銷要求它是 true。</summary>
    private bool RecentlyVerified()
        => HttpContext.Items.TryGetValue(AdminSessionFilter.RecentlyVerifiedKey, out var value)
           && value is true;

    [HttpGet("dashboard")]
    public ActionResult<ApiResponse<AdminUserService.Dashboard>> Dashboard()
    {
        if (Denied()) return NotFound();
        // 排程（restore-suspensions.yml）每小時跑一次，這裡再順手跑一次：
        // 審核的人正要看的就是這一頁的數字，不該讓它停在上一輪排程的舊狀態。
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

        // 用「實際效果」而不是「動作名稱」判斷要不要再驗證。
        //
        // SUSPEND 不帶到期日時走的是和 BAN 完全相同的程式路徑（ChangeStatus 的
        // disableLogin: true，而且註解自己就寫著「需人工解除」），也就是永久停權。
        // 只列 BAN/DELETE 的話，把 request body 改一個字就繞過去了。
        var permanentSuspend = request.Action == SanctionAction.SUSPEND && request.ExpiresAt == null;
        var destructive = request.Action is SanctionAction.BAN or SanctionAction.DELETE || permanentSuspend;
        if (destructive && !RecentlyVerified())
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<bool>.Fail(
                "ADMIN_REVERIFY_REQUIRED", "這個動作無法復原，請重新輸入信箱驗證碼。"));
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
