using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/matches")]
public sealed class MatchesController(MatchService matchService) : ControllerBase
{
    [HttpGet("today/{userId}")]
    public ActionResult<ApiResponse<List<MatchResponse>>> Today(string userId)
    {
        return ApiResponse<List<MatchResponse>>.Ok(matchService.GetTodayMatches(userId));
    }

    [HttpPost("{matchId}/like")]
    public ActionResult<ApiResponse<MatchRecord>> Like(string matchId)
    {
        var match = matchService.Like(matchId);
        return match == null
            ? NotFound(ApiResponse<MatchRecord>.Fail("MATCH_NOT_FOUND", "找不到配對。"))
            : ApiResponse<MatchRecord>.Ok(match);
    }
}
