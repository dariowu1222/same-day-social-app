using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/profile")]
public sealed class ProfileController(JsonStorageService storage, IServiceProvider services) : ControllerBase
{
    private readonly AppDbContext? db = services.GetService<AppDbContext>();

    [HttpGet("{userId}")]
    public async Task<ActionResult<ApiResponse<User>>> Get(string userId, CancellationToken cancellationToken)
    {
        if (db != null)
        {
            var record = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
            if (record != null) return ApiResponse<User>.Ok(record.ToDomain());
        }

        var user = storage.FindById<User>("users", userId);
        return user == null
            ? NotFound(ApiResponse<User>.Fail("USER_NOT_FOUND", "找不到使用者。"))
            : ApiResponse<User>.Ok(user);
    }

    [Authorize]
    [HttpPut("{userId}")]
    public async Task<ActionResult<ApiResponse<User>>> Update(string userId, [FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var callerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;
        if (callerId != userId)
            return Forbid();

        if (db != null)
        {
            var record = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
            if (record != null)
            {
                if (!string.IsNullOrWhiteSpace(request.Nickname)) record.Nickname = request.Nickname.Trim();
                if (request.Bio != null) record.Bio = request.Bio.Trim();
                if (request.InterestTags != null) record.InterestTags = [.. request.InterestTags];
                if (request.ValueTags != null) record.ValueTags = [.. request.ValueTags];
                if (request.PhotoDataUrls != null) record.PhotoDataUrls = [.. request.PhotoDataUrls];
                record.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return ApiResponse<User>.Ok(record.ToDomain());
            }
        }

        var user = storage.UpdateOne<User>("users", userId, target =>
        {
            target.Nickname = request.Nickname?.Trim() ?? target.Nickname;
            target.Bio = request.Bio?.Trim() ?? target.Bio;
            target.InterestTags = request.InterestTags ?? target.InterestTags;
            target.ValueTags = request.ValueTags ?? target.ValueTags;
            target.PhotoDataUrls = request.PhotoDataUrls ?? target.PhotoDataUrls;
        });

        return user == null
            ? NotFound(ApiResponse<User>.Fail("USER_NOT_FOUND", "找不到使用者。"))
            : ApiResponse<User>.Ok(user);
    }
}

public sealed record UpdateProfileRequest(string? Nickname, string? Bio, List<string>? InterestTags, List<string>? ValueTags, List<string>? PhotoDataUrls = null);
