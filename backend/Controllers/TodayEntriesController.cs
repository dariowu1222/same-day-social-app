using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/today-entries")]
[Authorize]
public sealed class TodayEntriesController(TodayEntryService todayEntryService) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    [HttpPost]
    public ActionResult<ApiResponse<object>> Create([FromBody] CreateTodayEntryRequest request)
    {
        // content 可以是空的。前端的文字欄本來就是選填——使用者只點一張心情卡
        // 就能送出（TodayEntryForm 的 canSubmit 只看 hasSelectedEntry），
        // 而這裡原本擋空字串回 400，再被前端的 silent catch 吞掉，
        // 結果是「只選心情不打字」的人今日一筆都沒存進去，自己毫不知情，
        // 也因此不會出現在任何人的配對牌堆裡。
        //
        // 空內容仍然有意義：心情卡選的 responseMode 會照常帶進去，
        // TodayAnalyzerService 對空字串會給 EventType.RANDOM + EmotionTag.CALM，
        // 不會丟例外。
        //
        // 長度上限倒是真的要擋：today_entries.content 是 varchar(2000)，
        // 超長會在 SaveChanges 丟出來變成 500。
        if (request.Content is { Length: > 2000 })
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID_REQUEST", "content 最多 2000 字。"));
        }

        // Content 宣告為不可為 null，但 JSON 送 null 一樣會進來，
        // 下游的 content.Trim() 會 NRE 變成 500——先前那個擋空字串的檢查
        // 順便也擋掉了 null，拿掉之後要自己補。
        var content = request.Content ?? string.Empty;

        // 以 token 的身分建立，忽略 body 傳來的 userId，避免冒名發文。
        var result = todayEntryService.Create(CallerId, content, request.ResponseMode, request.Visibility);
        return ApiResponse<object>.Ok(new { entry = result.Entry, analysis = result.Analysis });
    }

    [HttpGet("user/{userId}")]
    public ActionResult<ApiResponse<List<TodayEntry>>> GetByUser(string userId)
    {
        // 今日記錄屬高度私密內容，僅本人可讀取。
        if (CallerId != userId) return Forbid();
        return ApiResponse<List<TodayEntry>>.Ok(todayEntryService.GetByUser(userId));
    }
}

public sealed record CreateTodayEntryRequest(
    string UserId,
    string Content,
    ResponseMode ResponseMode,
    Visibility Visibility);
