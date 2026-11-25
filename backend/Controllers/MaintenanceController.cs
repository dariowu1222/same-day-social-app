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

public sealed record ChatExpiryResult(int Notified, int Expired, int Scanned, bool StorageReady);

/// <summary>Redacted = 這次清空了幾筆。Ran = false 代表沒有資料庫，什麼都沒做。</summary>
public sealed record PurgeReportContentResult(int Redacted, bool Ran);

/// <summary>
/// Replies = 刪掉幾則收回的回應、Posts = 刪掉幾篇貼文、Redacted = 清空幾篇底下還有回應的貼文。
/// Ran = false 代表沒有資料庫，什麼都沒做。
/// </summary>
public sealed record PurgeDeletedRantsResult(int Replies, int Posts, int Redacted, bool Ran);

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
    ChatExpiryService chatExpiry,
    ReportService reports,
    RantService rants,
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
                "MAINTENANCE_DISABLED", "維護端點未啟用"));
        }

        if (!FixedTimeEquals(secret, expected))
        {
            logger.LogWarning("停權恢復端點收到錯誤的密鑰");
            return Unauthorized(ApiResponse<RestoreExpiredSuspensionsResult>.Fail("UNAUTHORIZED", "密鑰不符"));
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

    /// <summary>
    /// 聊天室閒置到期：先通知、隔天才鎖定。
    ///
    /// 為什麼跟停權恢復共用同一把密鑰：兩者都是「這台排程機器可以叫後端做維護」
    /// 這一件事，外洩的後果也相同（有人能重複觸發維護）。再開一把只會多一個
    /// 要輪替的祕密。與 Push__TriggerSecret 分開則是刻意的——那條鏈路會真的
    /// 對使用者送出推播，風險不同級。
    ///
    /// 可以重複打：通知過的不會再通知，鎖定過的不會再掃到。
    /// </summary>
    [AllowAnonymous]
    [HttpPost("chat-expiry")]
    public ActionResult<ApiResponse<ChatExpiryResult>> RunChatExpiry(
        [FromHeader(Name = "X-Maintenance-Trigger")] string? secret)
    {
        var expected = options.Value.TriggerSecret;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return NotFound(ApiResponse<ChatExpiryResult>.Fail("MAINTENANCE_DISABLED", "維護端點未啟用"));
        }

        if (!FixedTimeEquals(secret, expected))
        {
            logger.LogWarning("聊天室到期端點收到錯誤的密鑰");
            return Unauthorized(ApiResponse<ChatExpiryResult>.Fail("UNAUTHORIZED", "密鑰不符"));
        }

        var outcome = chatExpiry.Run();
        return ApiResponse<ChatExpiryResult>.Ok(new ChatExpiryResult(
            outcome.Notified, outcome.Expired, outcome.Scanned, outcome.StorageReady));
    }


    /// <summary>
    /// 清掉處理完超過 30 天的檢舉內容（detail 與 context_snapshot）。
    ///
    /// 清空而不是刪列：聊天室的凍結與推播靜音都是「reports 裡還存在那一列
    /// RESOLVED」即時算出來的，刪掉等於解除對騷擾者的處置，而受害者不會
    /// 收到任何通知。詳見 ReportService.PurgeOldOutcomes 的說明。
    ///
    /// 可以重複打：已經清空的列不會再被掃到。
    /// </summary>
    [AllowAnonymous]
    [HttpPost("purge-report-content")]
    public ActionResult<ApiResponse<PurgeReportContentResult>> PurgeReportContent(
        [FromHeader(Name = "X-Maintenance-Trigger")] string? secret)
    {
        var expected = options.Value.TriggerSecret;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return NotFound(ApiResponse<PurgeReportContentResult>.Fail("MAINTENANCE_DISABLED", "維護端點未啟用"));
        }

        if (!FixedTimeEquals(secret, expected))
        {
            logger.LogWarning("檢舉內容清理端點收到錯誤的密鑰");
            return Unauthorized(ApiResponse<PurgeReportContentResult>.Fail("UNAUTHORIZED", "密鑰不符"));
        }

        var outcome = reports.PurgeOldOutcomes();
        if (outcome.Redacted > 0)
        {
            logger.LogInformation("清掉 {Count} 筆逾期檢舉的內容欄位", outcome.Redacted);
        }

        return ApiResponse<PurgeReportContentResult>.Ok(
            new PurgeReportContentResult(outcome.Redacted, outcome.Ran));
    }

    /// <summary>
    /// 把刪除／收回超過 30 天的樹洞內容真的清掉。
    ///
    /// 使用者按下刪除貼文、收回回應的當下，內容只是對所有人隱形，列還留著——
    /// 那 30 天是留給檢舉與後台查證用的（隱私權政策上寫的就是這個數字）。
    /// 這支不跑的話，「刪除」就永遠只是隱藏，政策上的承諾會落空。
    ///
    /// 可以重複打：清過的不會再被掃到。細節見 RantService.PurgeDeleted。
    /// </summary>
    [AllowAnonymous]
    [HttpPost("purge-deleted-rants")]
    public ActionResult<ApiResponse<PurgeDeletedRantsResult>> PurgeDeletedRants(
        [FromHeader(Name = "X-Maintenance-Trigger")] string? secret)
    {
        var expected = options.Value.TriggerSecret;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return NotFound(ApiResponse<PurgeDeletedRantsResult>.Fail("MAINTENANCE_DISABLED", "維護端點未啟用"));
        }

        if (!FixedTimeEquals(secret, expected))
        {
            logger.LogWarning("樹洞刪除內容清理端點收到錯誤的密鑰");
            return Unauthorized(ApiResponse<PurgeDeletedRantsResult>.Fail("UNAUTHORIZED", "密鑰不符"));
        }

        var outcome = rants.PurgeDeleted();
        if (outcome.Replies + outcome.Posts + outcome.Redacted > 0)
        {
            logger.LogInformation(
                "樹洞保留期滿：刪掉 {Replies} 則回應、{Posts} 篇貼文，清空 {Redacted} 篇貼文的內容",
                outcome.Replies, outcome.Posts, outcome.Redacted);
        }

        return ApiResponse<PurgeDeletedRantsResult>.Ok(
            new PurgeDeletedRantsResult(outcome.Replies, outcome.Posts, outcome.Redacted, outcome.Ran));
    }

    private static bool FixedTimeEquals(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}
