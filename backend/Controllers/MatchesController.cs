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
        var outcome = matchService.Like(matchId, CallerId);
        return outcome.Status switch
        {
            LikeStatus.Ok => ApiResponse<LikeResult>.Ok(outcome.Result!),

            // 410 而不是 404：這筆配對存在、也確實是你的，只是另一邊的人已經不在了。
            // 前端靠 MATCH_UNAVAILABLE 區分「暫時送不出去、等下補送」與「送幾次都一樣」。
            LikeStatus.CandidateUnavailable => StatusCode(
                StatusCodes.Status410Gone,
                ApiResponse<LikeResult>.Fail("MATCH_UNAVAILABLE", "這個人已經不在了。")),

            _ => NotFound(ApiResponse<LikeResult>.Fail("MATCH_NOT_FOUND", "找不到這筆配對。")),
        };
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
