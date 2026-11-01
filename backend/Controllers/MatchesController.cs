using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/matches")]
[Authorize]
public sealed class MatchesController(MatchService matchService) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    [HttpGet("today/{userId}")]
    public ActionResult<ApiResponse<List<MatchResponse>>> Today(string userId)
    {
        if (CallerId != userId) return Forbid();
        return ApiResponse<List<MatchResponse>>.Ok(matchService.GetTodayMatches(userId));
    }

    [EnableRateLimiting("write")]
    [HttpPost("{matchId}/like")]
    public ActionResult<ApiResponse<LikeResult>> Like(string matchId)
    {
        var result = matchService.Like(matchId, CallerId);
        return result == null
            ? NotFound(ApiResponse<LikeResult>.Fail("MATCH_NOT_FOUND", "找不到這筆配對。"))
            : ApiResponse<LikeResult>.Ok(result);
    }

    /// <summary>
    /// 左滑（略過）。與 like 分開，因為 like 還要判互相、開聊天室、發通知、
    /// 回 LikeResult，塞進同一支會讓回傳型別變成可空的大雜燴。
    /// </summary>
    [EnableRateLimiting("write")]
    [HttpPost("{matchId}/pass")]
    public ActionResult<ApiResponse<bool>> Pass(string matchId)
    {
        return matchService.Pass(matchId, CallerId)
            ? ApiResponse<bool>.Ok(true)
            : NotFound(ApiResponse<bool>.Fail("MATCH_NOT_FOUND", "找不到這筆配對。"));
    }
}
