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
            : NotFound(ApiResponse<bool>.Fail("ACCOUNT_NOT_FOUND", "找不到帳號"));
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
            : NotFound(ApiResponse<bool>.Fail("BLOCK_NOT_FOUND", "找不到封鎖紀錄"));
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

    // 條款同意與年齡確認的補問。
    // 以前用 Google／Facebook 建的帳號什麼都沒問過；新版 App 登入後先用 GET 問「這個帳號
    // 還缺不缺」，缺就顯示確認畫面，使用者確認後用 POST 記下來。
    // 兩支都只作用在呼叫者自己身上（沒有 userId 參數），沒有「幫別人同意」這件事。
    [HttpGet("consent")]
    public async Task<ActionResult<ApiResponse<ConsentStatus>>> Consent(CancellationToken cancellationToken)
        => ToActionResult(await authService.GetConsentStatusAsync(CallerId, cancellationToken));

    [HttpPost("consent")]
    public async Task<ActionResult<ApiResponse<ConsentStatus>>> GiveConsent(
        [FromBody] GiveConsentRequest request, CancellationToken cancellationToken)
        => ToActionResult(await authService.RecordConsentAsync(
            CallerId, request.TermsAccepted, request.BirthYear, cancellationToken));

    private ActionResult<ApiResponse<ConsentStatus>> ToActionResult(AuthServiceResult<ConsentStatus> result)
        => result.Success
            ? ApiResponse<ConsentStatus>.Ok(result.Data!)
            : BadRequest(ApiResponse<ConsentStatus>.Fail(result.Code!, result.Message!));
}

public sealed record UpdateUserSettingRequest(
    bool? ProfilePublic = null, bool? PauseMatching = null,
    bool? NotifyMatch = null, bool? NotifyMessage = null, bool? NotifyRant = null);

public sealed record GiveConsentRequest(bool TermsAccepted = false, string? BirthYear = null);
