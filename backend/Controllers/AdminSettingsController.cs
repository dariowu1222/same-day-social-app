using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Infrastructure.Security;

namespace SameDaySocialApp.Controllers;

/// <summary>
/// 後台的營運規則設定。與 AdminUsersController 共用同一套守門原則：
/// 預設拒絕、未授權回 404（不洩漏端點存在）、每次更動寫入審核軌跡。
///
/// 路由必須掛在 api/admin 底下：Program.cs 有一段中介軟體擋「後台 token 只能打
/// 後台路徑」，取別的前綴會被回 401 ADMIN_TOKEN_SCOPE，而前端看起來像登入過期。
///
/// 為什麼要有審核軌跡：這兩個值會直接改變使用者看得到的行為——天數調短會讓一批
/// 聊天室在下一次排程被鎖，上限調低會讓人配不到新的對象。事後要查得出是誰改的。
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
[ServiceFilter(typeof(AdminSessionFilter))]
public sealed class AdminSettingsController(
    AppSettingsService settings,
    ChatExpiryService chatExpiry,
    ReportService reports,
    AdminAccessService admin) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    // filter 已經擋過一次，這裡再擋一次是刻意的：守門邏輯不該只有一個地方知道，
    // 哪天 filter 被誰從類別上拿掉，端點也不會就這樣敞開。
    private bool Denied() => !admin.IsAdmin(CallerId);

    /// <summary>
    /// 目前生效的規則，以及每個值的合法範圍。
    ///
    /// 範圍由後端給、不要寫死在前端：驗證的真相只能有一份。前端自己抄一份的話，
    /// 哪天後端放寬了，畫面會繼續擋著不給改。
    /// </summary>
    public sealed record Settings(
        int ChatIdleDays, int ChatIdleDaysMin, int ChatIdleDaysMax,
        int ChatMaxPartners, int ChatMaxPartnersMin, int ChatMaxPartnersMax,
        DateTimeOffset? ChatExpirySince);

    public sealed record UpdateRequest(int? ChatIdleDays, int? ChatMaxPartners);

    [HttpGet("settings")]
    public ActionResult<ApiResponse<Settings>> Get()
    {
        if (Denied()) return NotFound();
        return ApiResponse<Settings>.Ok(Current());
    }

    [HttpPut("settings")]
    public ActionResult<ApiResponse<Settings>> Update([FromBody] UpdateRequest request)
    {
        if (Denied()) return NotFound();

        var changes = new List<string>();

        if (request.ChatIdleDays is { } days)
        {
            if (!settings.TrySet(AppSettingsService.IdleDaysKey, days.ToString(), CallerId))
            {
                return BadRequest(ApiResponse<Settings>.Fail(
                    "INVALID_VALUE",
                    $"閒置天數必須介於 {AppSettingsService.MinIdleDays} 到 {AppSettingsService.MaxIdleDays} 之間。"));
            }
            changes.Add($"閒置天數={days}");
        }

        if (request.ChatMaxPartners is { } partners)
        {
            if (!settings.TrySet(AppSettingsService.MaxPartnersKey, partners.ToString(), CallerId))
            {
                return BadRequest(ApiResponse<Settings>.Fail(
                    "INVALID_VALUE",
                    $"聊天對象上限必須介於 {AppSettingsService.MinMaxPartners} 到 {AppSettingsService.MaxMaxPartners} 之間。"));
            }
            changes.Add($"聊天對象上限={partners}");
        }

        if (changes.Count > 0)
        {
            reports.LogAudit(CallerId, "UPDATE_APP_SETTINGS", null, string.Join("、", changes));
        }

        return ApiResponse<Settings>.Ok(Current());
    }

    /// <summary>
    /// 手動跑一次聊天室到期處理。
    ///
    /// 排程每天早上九點跑一次就夠，但調完天數之後常常會想立刻看看會影響幾間房——
    /// 等到隔天早上才知道調錯了，中間那批已經被鎖掉了。
    /// 這支做的事跟排程那支完全一樣（同一個 service），不是另一條路徑。
    /// </summary>
    [HttpPost("settings/run-chat-expiry")]
    public ActionResult<ApiResponse<ChatExpiryService.Outcome>> RunChatExpiry()
    {
        if (Denied()) return NotFound();
        var outcome = chatExpiry.Run();
        reports.LogAudit(CallerId, "RUN_CHAT_EXPIRY", null,
            $"掃描 {outcome.Scanned} 間，通知 {outcome.Notified} 間，鎖定 {outcome.Expired} 間");
        return ApiResponse<ChatExpiryService.Outcome>.Ok(outcome);
    }

    private Settings Current() => new(
        settings.IdleDays, AppSettingsService.MinIdleDays, AppSettingsService.MaxIdleDays,
        settings.MaxPartners, AppSettingsService.MinMaxPartners, AppSettingsService.MaxMaxPartners,
        settings.ExpirySince);
}
