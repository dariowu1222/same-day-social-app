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

    // 個人資料會顯示在配對 / 聊天中，故任何登入者皆可讀取（但需登入，避免匿名爬取列舉）。
    [Authorize]
    [HttpGet("{userId}")]
    public async Task<ActionResult<ApiResponse<User>>> Get(string userId, CancellationToken cancellationToken)
    {
        if (db != null)
        {
            try
            {
                var record = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
                if (record != null) return ApiResponse<User>.Ok(record.ToDomain());
            }
            catch { /* DB 欄位尚未 migrate，fallback 到 JSON */ }
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

        if (request.Birthday.HasValue && !IsAdult(request.Birthday.Value))
        {
            return BadRequest(ApiResponse<User>.Fail("UNDERAGE", "未滿 18 歲暫時不能使用同頻 Today。"));
        }

        if (db != null)
        {
            try
            {
                var record = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
                if (record != null)
                {
                    if (!string.IsNullOrWhiteSpace(request.Nickname)) record.Nickname = request.Nickname.Trim();
                    if (request.Bio != null) record.Bio = request.Bio.Trim();
                    if (request.InterestTags != null) record.InterestTags = [.. request.InterestTags];
                    if (request.ValueTags != null) record.ValueTags = [.. request.ValueTags];
                    if (request.PhotoDataUrls != null) record.PhotoDataUrls = [.. request.PhotoDataUrls];
                    if (request.Birthday.HasValue) record.Birthday = request.Birthday;
                    record.UpdatedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    return ApiResponse<User>.Ok(record.ToDomain());
                }
            }
            catch { /* DB 欄位尚未 migrate，fallback 到 JSON */ }
        }

        var user = storage.UpdateOne<User>("users", userId, target =>
        {
            target.Nickname = request.Nickname?.Trim() ?? target.Nickname;
            target.Bio = request.Bio?.Trim() ?? target.Bio;
            target.InterestTags = request.InterestTags ?? target.InterestTags;
            target.ValueTags = request.ValueTags ?? target.ValueTags;
            target.PhotoDataUrls = request.PhotoDataUrls ?? target.PhotoDataUrls;
            target.Birthday = request.Birthday ?? target.Birthday;
        });

        return user == null
            ? NotFound(ApiResponse<User>.Fail("USER_NOT_FOUND", "找不到使用者。"))
            : ApiResponse<User>.Ok(user);
    }

    private static bool IsAdult(DateOnly birthday)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.DateTime);
        return birthday.AddYears(18) <= today;
    }
}

public sealed record UpdateProfileRequest(string? Nickname, string? Bio, List<string>? InterestTags, List<string>? ValueTags, List<string>? PhotoDataUrls = null, DateOnly? Birthday = null);
