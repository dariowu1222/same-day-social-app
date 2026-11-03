using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;

namespace SameDaySocialApp.Controllers;

/// <summary>排程觸發端點的設定。沒設密鑰就等於這些端點沒開。</summary>
public sealed class MaintenanceOptions
{
    /// <summary>共享密鑰。刻意不跟 Push__TriggerSecret 共用：兩者外洩的後果不同，
    /// 綁在一起的話換掉其中一個就得同時改另一條排程。</summary>
    public string? TriggerSecret { get; set; }
}

public sealed record RestoreExpiredSuspensionsResult(int Restored);

/// <summary>
/// 由排程打的維護端點。
///
/// 為什麼不是後端自己跑 BackgroundService：
/// 跟彙總推播同一個理由（見 .github/workflows/push-digest.yml）——Render 免費方案
/// 閒置 15 分鐘就休眠、行程整個停掉，PeriodicTimer 不會跑。後端只提供端點，
/// 排程交給 GitHub Actions。
///
/// 為什麼不用 JWT：呼叫者不是任何一個使用者，所以用共享密鑰，比對走固定時間。
/// </summary>
[ApiController]
[Route("api/maintenance")]
public sealed class MaintenanceController(
    AdminUserService users,
    IOptions<MaintenanceOptions> options,
    ILogger<MaintenanceController> logger) : ControllerBase
{
    /// <summary>
    /// 把到期的停權寫回 ACTIVE。
    ///
    /// 登入與讀取兩條路現在都自己看 suspended_until（見 AccountStateService.IsActive），
    /// 所以期滿的人不必等這支跑完就能回來——這支要修的是資料庫裡那份狀態本身。
    /// 不跑的話 users.status 會一直停在 SUSPENDED，而後台的會員列表、狀態篩選與
    /// 儀表板計數是直接查那個欄位的：畫面上寫著停權中，人其實早就回來了，
    /// 審核的人只能照著錯的數字做判斷。
    ///
    /// 可以重複打：只挑「有期限而且已經到期」的列，跑第二次就是 0 筆。
    /// </summary>
    [AllowAnonymous]
    [HttpPost("restore-expired-suspensions")]
    public ActionResult<ApiResponse<RestoreExpiredSuspensionsResult>> RestoreExpiredSuspensions(
        [FromHeader(Name = "X-Maintenance-Trigger")] string? secret)
    {
        var expected = options.Value.TriggerSecret;
        if (string.IsNullOrWhiteSpace(expected))
        {
            // 沒設密鑰等於這支沒開，不要讓端點變成誰都能打
            return NotFound(ApiResponse<RestoreExpiredSuspensionsResult>.Fail(
                "MAINTENANCE_DISABLED", "維護端點未啟用。"));
        }

        if (!FixedTimeEquals(secret, expected))
        {
            logger.LogWarning("停權恢復端點收到錯誤的密鑰");
            return Unauthorized(ApiResponse<RestoreExpiredSuspensionsResult>.Fail("UNAUTHORIZED", "密鑰不符。"));
        }

        var restored = users.RestoreExpiredSuspensions();
        if (restored > 0)
        {
            // 留一行紀錄：停權自己到期解除時沒有任何人按過按鈕，
            // 之後要回頭查「他是什麼時候回來的」就只剩這裡和 account_sanctions。
            logger.LogInformation("停權期滿自動恢復 {Count} 個帳號", restored);
        }

        return ApiResponse<RestoreExpiredSuspensionsResult>.Ok(new RestoreExpiredSuspensionsResult(restored));
    }

    private static bool FixedTimeEquals(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}
