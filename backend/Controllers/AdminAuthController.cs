using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Infrastructure.Security;

namespace SameDaySocialApp.Controllers;

/// <summary>
/// 進入後台的第二道關卡。
///
/// 這裡的所有端點都用「一般 App token」驗身分（所以要先登入 App 或後台登入頁），
/// 但它們本身不是後台端點——它們是換發後台 token 的地方。
///
/// 非白名單使用者一律 404：連「有這支端點」都不該讓人知道。
/// </summary>
[ApiController]
[Route("api/admin/auth")]
[Authorize]
[EnableRateLimiting("auth")]
public sealed class AdminAuthController(
    AdminSessionService sessions,
    AdminAccessService admin) : ControllerBase
{
    private string? CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                                ?? User.FindFirst("sub")?.Value;

    private string CallerNickname => User.FindFirst("nickname")?.Value ?? "管理者";

    /// <summary>
    /// App 用來決定要不要顯示「管理後台」那一列。
    ///
    /// 只回「是不是管理者」，不回任何名單內容。不是的話回 404，
    /// 所以一般使用者的 App 連這個概念都接觸不到。
    /// </summary>
    [HttpGet("~/api/admin/me")]
    // 每個使用者登入 App 都會打這支。掛在 auth 政策下的話，它會跟後台的
    // 驗證碼流程共用同一個 10 次/分的桶，彼此互相排擠。
    [DisableRateLimiting]
    public ActionResult<ApiResponse<AdminMeResponse>> Me()
    {
        if (!admin.IsAdmin(CallerId))
        {
            return NotFound(ApiResponse<AdminMeResponse>.Fail("NOT_FOUND", "找不到此資源。"));
        }
        return ApiResponse<AdminMeResponse>.Ok(new AdminMeResponse(true));
    }

    /// <summary>寄出進入後台的驗證碼。</summary>
    [HttpPost("challenge")]
    public async Task<ActionResult<ApiResponse<AdminChallengeResponse>>> Challenge(CancellationToken ct)
    {
        var userId = CallerId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return NotFound(ApiResponse<AdminChallengeResponse>.Fail("NOT_FOUND", "找不到此資源。"));
        }

        var result = await sessions.SendChallengeAsync(userId, AdminSessionService.CodePurpose.Login, ct);
        return result.Failure switch
        {
            AdminSessionService.Failure.None =>
                ApiResponse<AdminChallengeResponse>.Ok(new AdminChallengeResponse(result.MaskedEmail!)),
            AdminSessionService.Failure.NotAdmin =>
                NotFound(ApiResponse<AdminChallengeResponse>.Fail("NOT_FOUND", "找不到此資源。")),
            AdminSessionService.Failure.NoEmail =>
                BadRequest(ApiResponse<AdminChallengeResponse>.Fail(
                    "ADMIN_NO_EMAIL", "這個帳號沒有可用的登入信箱，無法進入後台。")),
            AdminSessionService.Failure.SmtpNotConfigured =>
                BadRequest(ApiResponse<AdminChallengeResponse>.Fail(
                    "SMTP_NOT_CONFIGURED", "尚未設定寄信服務，無法寄出後台驗證碼。")),
            AdminSessionService.Failure.TooManyRequests =>
                StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<AdminChallengeResponse>.Fail(
                    "ADMIN_CODE_THROTTLED", "驗證碼寄送太頻繁，請稍候再試。")),
            _ =>
                BadRequest(ApiResponse<AdminChallengeResponse>.Fail(
                    "ADMIN_CHALLENGE_FAILED", "驗證碼寄送失敗，請稍後再試。")),
        };
    }

    /// <summary>驗證碼正確就換到後台 token。</summary>
    [HttpPost("verify")]
    public async Task<ActionResult<ApiResponse<AdminTokenResponse>>> Verify(
        [FromBody] AdminVerifyRequest request, CancellationToken ct)
    {
        var userId = CallerId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return NotFound(ApiResponse<AdminTokenResponse>.Fail("NOT_FOUND", "找不到此資源。"));
        }

        var result = await sessions.VerifyAsync(
            userId, CallerNickname, request.Code ?? "",
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            ct);

        return result.Failure switch
        {
            AdminSessionService.Failure.None =>
                ApiResponse<AdminTokenResponse>.Ok(new AdminTokenResponse(result.Token!, result.ExpiresAt!.Value)),
            AdminSessionService.Failure.NotAdmin =>
                NotFound(ApiResponse<AdminTokenResponse>.Fail("NOT_FOUND", "找不到此資源。")),
            AdminSessionService.Failure.CodeExpired =>
                BadRequest(ApiResponse<AdminTokenResponse>.Fail("CODE_EXPIRED", "驗證碼已過期，請重新取得。")),
            AdminSessionService.Failure.TooManyAttempts =>
                BadRequest(ApiResponse<AdminTokenResponse>.Fail("CODE_TOO_MANY_ATTEMPTS", "錯誤次數過多，請重新取得驗證碼。")),
            _ =>
                BadRequest(ApiResponse<AdminTokenResponse>.Fail("CODE_INVALID", "驗證碼不正確。")),
        };
    }

    /// <summary>
    /// 要一組「再驗證」專用的碼。要帶後台 token，而且寄出的碼只能用於確認處置、
    /// 不能用來登入——信裡也會寫明這件事。
    /// </summary>
    [HttpPost("reverify-challenge")]
    [ServiceFilter(typeof(AdminSessionFilter))]
    public async Task<ActionResult<ApiResponse<AdminChallengeResponse>>> ReverifyChallenge(CancellationToken ct)
    {
        var result = await sessions.SendChallengeAsync(
            CallerId!, AdminSessionService.CodePurpose.Reverify, ct);

        return result.Failure switch
        {
            AdminSessionService.Failure.None =>
                ApiResponse<AdminChallengeResponse>.Ok(new AdminChallengeResponse(result.MaskedEmail!)),
            AdminSessionService.Failure.TooManyRequests =>
                StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<AdminChallengeResponse>.Fail(
                    "ADMIN_CODE_THROTTLED", "驗證碼寄送太頻繁，請稍候再試。")),
            _ =>
                BadRequest(ApiResponse<AdminChallengeResponse>.Fail(
                    "ADMIN_CHALLENGE_FAILED", "驗證碼寄送失敗，請稍後再試。")),
        };
    }

    /// <summary>
    /// 破壞性動作前的再驗證。通過後十分鐘內可以執行封鎖／註銷。
    /// 這支要帶後台 token，所以掛 filter。
    /// </summary>
    [HttpPost("reverify")]
    [ServiceFilter(typeof(AdminSessionFilter))]
    public async Task<ActionResult<ApiResponse<bool>>> Reverify(
        [FromBody] AdminVerifyRequest request, CancellationToken ct)
    {
        var userId = CallerId!;
        var sessionId = User.FindFirst(JwtService.AdminSessionClaim)!.Value;

        var failure = await sessions.ReverifyAsync(userId, sessionId, request.Code ?? "", ct);
        return failure switch
        {
            AdminSessionService.Failure.None => ApiResponse<bool>.Ok(true),
            AdminSessionService.Failure.CodeExpired =>
                BadRequest(ApiResponse<bool>.Fail("CODE_EXPIRED", "驗證碼已過期，請重新取得。")),
            AdminSessionService.Failure.TooManyAttempts =>
                BadRequest(ApiResponse<bool>.Fail("CODE_TOO_MANY_ATTEMPTS", "錯誤次數過多，請重新取得驗證碼。")),
            _ => BadRequest(ApiResponse<bool>.Fail("CODE_INVALID", "驗證碼不正確。")),
        };
    }

    /// <summary>登出後台。session 當場撤銷，那顆 token 立刻失效。</summary>
    [HttpPost("logout")]
    [ServiceFilter(typeof(AdminSessionFilter))]
    public async Task<ActionResult<ApiResponse<bool>>> Logout(CancellationToken ct)
    {
        var sessionId = User.FindFirst(JwtService.AdminSessionClaim)!.Value;
        await sessions.RevokeAsync(sessionId, ct);
        return ApiResponse<bool>.Ok(true);
    }

    /// <summary>
    /// 撤銷自己名下所有後台 session。裝置遺失、或懷疑 token 外洩時用——
    /// 這是純 JWT 做不到、所以才需要 admin_sessions 那張表的主要理由。
    /// </summary>
    [HttpPost("logout-all")]
    [ServiceFilter(typeof(AdminSessionFilter))]
    public async Task<ActionResult<ApiResponse<bool>>> LogoutAll(CancellationToken ct)
    {
        await sessions.RevokeAllAsync(CallerId!, ct);
        return ApiResponse<bool>.Ok(true);
    }

    /// <summary>
    /// 把另一位管理者的後台 session 全部切掉。
    ///
    /// 出事時最需要的動作就是這個，而它不該要等改環境變數、重新部署才做得到。
    /// 對象必須也在白名單上——這支不是用來處置一般使用者的，那走 sanction。
    /// </summary>
    [HttpPost("revoke-sessions")]
    [ServiceFilter(typeof(AdminSessionFilter))]
    public async Task<ActionResult<ApiResponse<bool>>> RevokeSessions(
        [FromBody] AdminRevokeRequest request, CancellationToken ct)
    {
        if (!admin.IsAdmin(request.UserId))
        {
            return BadRequest(ApiResponse<bool>.Fail("NOT_ADMIN", "對象不是管理者。"));
        }
        await sessions.RevokeAllAsync(request.UserId, ct);
        return ApiResponse<bool>.Ok(true);
    }
}

public sealed record AdminMeResponse(bool IsAdmin);
public sealed record AdminRevokeRequest(string UserId);
public sealed record AdminChallengeResponse(string MaskedEmail);
public sealed record AdminTokenResponse(string Token, DateTimeOffset ExpiresAt);
public sealed record AdminVerifyRequest(string? Code);
