using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;

namespace SameDaySocialApp.Controllers;

public sealed record RegisterPushTokenRequest(string Token, string? Platform);

[ApiController]
[Route("api/push")]
public sealed class PushController(
    PushService pushService,
    IOptions<PushOptions> options,
    ILogger<PushController> logger) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    /// <summary>登入後把這台裝置的 FCM token 送上來。token 會輪替，所以每次啟動都要重送。</summary>
    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost("tokens")]
    public ActionResult<ApiResponse<bool>> Register([FromBody] RegisterPushTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(ApiResponse<bool>.Fail("INVALID_TOKEN", "缺少裝置識別碼。"));
        }

        pushService.RegisterToken(CallerId, request.Token.Trim(), request.Platform?.Trim() ?? "android");
        return ApiResponse<bool>.Ok(true);
    }

    /// <summary>登出時解除綁定，免得下一個登入這台裝置的人收到前一個人的推播。</summary>
    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost("tokens/remove")]
    public ActionResult<ApiResponse<bool>> Unregister([FromBody] RegisterPushTokenRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            pushService.UnregisterToken(CallerId, request.Token.Trim());
        }

        return ApiResponse<bool>.Ok(true);
    }

    /// <summary>
    /// 排程觸發的彙總推播。由 GitHub Actions 每兩小時打一次。
    ///
    /// 不用 JWT 而用共享密鑰，因為呼叫者不是任何一個使用者。
    /// 比較用固定時間比對，避免從回應時間反推密鑰。
    /// </summary>
    [AllowAnonymous]
    [HttpPost("digest")]
    public async Task<ActionResult<ApiResponse<PushDigestResult>>> Digest(
        [FromHeader(Name = "X-Push-Trigger")] string? secret, CancellationToken ct)
    {
        var expected = options.Value.TriggerSecret;
        if (string.IsNullOrWhiteSpace(expected))
        {
            // 沒設密鑰等於這個功能沒開，不要讓端點變成誰都能打
            return NotFound(ApiResponse<PushDigestResult>.Fail("PUSH_DISABLED", "推播未啟用。"));
        }

        if (!FixedTimeEquals(secret, expected))
        {
            logger.LogWarning("彙總推播端點收到錯誤的密鑰");
            return Unauthorized(ApiResponse<PushDigestResult>.Fail("UNAUTHORIZED", "密鑰不符。"));
        }

        return ApiResponse<PushDigestResult>.Ok(await pushService.RunDigestAsync(ct));
    }

    private static bool FixedTimeEquals(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}
