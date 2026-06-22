using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpPost("{matchId}/like")]
    public ActionResult<ApiResponse<MatchRecord>> Like(string matchId)
    {
        var match = matchService.Like(matchId, CallerId);
        return match == null
            ? NotFound(ApiResponse<MatchRecord>.Fail("MATCH_NOT_FOUND", "找不到這筆配對。"))
            : ApiResponse<MatchRecord>.Ok(match);
    }
}
