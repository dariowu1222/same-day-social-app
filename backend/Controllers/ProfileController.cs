using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/profile")]
public sealed class ProfileController(JsonStorageService storage) : ControllerBase
{
    [HttpGet("{userId}")]
    public ActionResult<ApiResponse<User>> Get(string userId)
    {
        var user = storage.FindById<User>("users", userId);
        return user == null
            ? NotFound(ApiResponse<User>.Fail("USER_NOT_FOUND", "找不到使用者。"))
            : ApiResponse<User>.Ok(user);
    }

    [HttpPut("{userId}")]
    public ActionResult<ApiResponse<User>> Update(string userId, [FromBody] UpdateProfileRequest request)
    {
        var user = storage.UpdateOne<User>("users", userId, target =>
        {
            target.Nickname = request.Nickname?.Trim() ?? target.Nickname;
            target.Bio = request.Bio?.Trim() ?? target.Bio;
            target.InterestTags = request.InterestTags ?? target.InterestTags;
            target.ValueTags = request.ValueTags ?? target.ValueTags;
        });

        return user == null
            ? NotFound(ApiResponse<User>.Fail("USER_NOT_FOUND", "找不到使用者。"))
            : ApiResponse<User>.Ok(user);
    }
}

public sealed record UpdateProfileRequest(string? Nickname, string? Bio, List<string>? InterestTags, List<string>? ValueTags);
