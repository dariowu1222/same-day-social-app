using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/today-entries")]
public sealed class TodayEntriesController(TodayEntryService todayEntryService) : ControllerBase
{
    [HttpPost]
    public ActionResult<ApiResponse<object>> Create([FromBody] CreateTodayEntryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID_REQUEST", "userId 與 content 為必填。"));
        }

        var result = todayEntryService.Create(request.UserId, request.Content, request.ResponseMode, request.Visibility);
        return ApiResponse<object>.Ok(new { entry = result.Entry, analysis = result.Analysis });
    }

    [HttpGet("user/{userId}")]
    public ActionResult<ApiResponse<List<TodayEntry>>> GetByUser(string userId)
    {
        return ApiResponse<List<TodayEntry>>.Ok(todayEntryService.GetByUser(userId));
    }
}

public sealed record CreateTodayEntryRequest(
    string UserId,
    string Content,
    ResponseMode ResponseMode,
    Visibility Visibility);
