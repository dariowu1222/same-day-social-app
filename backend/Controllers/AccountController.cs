using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController(AuthService authService) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    // 刪除帳號（軟刪除：停用登入並清個資）
    [HttpDelete]
    public ActionResult<ApiResponse<bool>> Delete()
    {
        var ok = authService.DeleteAccount(CallerId);
        return ok
            ? ApiResponse<bool>.Ok(true)
            : NotFound(ApiResponse<bool>.Fail("ACCOUNT_NOT_FOUND", "找不到帳號。"));
    }

    // 安全中心：封鎖名單
    [HttpGet("blocks")]
    public ActionResult<ApiResponse<List<BlockedUser>>> Blocks()
        => ApiResponse<List<BlockedUser>>.Ok(authService.GetBlockedUsers(CallerId));

    [HttpDelete("blocks/{blockedId}")]
    public ActionResult<ApiResponse<bool>> Unblock(string blockedId)
    {
        var ok = authService.Unblock(CallerId, blockedId);
        return ok
            ? ApiResponse<bool>.Ok(true)
            : NotFound(ApiResponse<bool>.Fail("BLOCK_NOT_FOUND", "找不到封鎖紀錄。"));
    }

    // 隱私 / 通知設定
    [HttpGet("settings")]
    public ActionResult<ApiResponse<UserSetting>> Settings()
        => ApiResponse<UserSetting>.Ok(authService.GetSettings(CallerId));

    [HttpPut("settings")]
    public ActionResult<ApiResponse<UserSetting>> UpdateSettings([FromBody] UpdateUserSettingRequest request)
        => ApiResponse<UserSetting>.Ok(authService.UpdateSettings(
            CallerId, request.ProfilePublic, request.PauseMatching,
            request.NotifyMatch, request.NotifyMessage, request.NotifyRant));
}

public sealed record UpdateUserSettingRequest(
    bool? ProfilePublic = null, bool? PauseMatching = null,
    bool? NotifyMatch = null, bool? NotifyMessage = null, bool? NotifyRant = null);
