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
                    if (!string.IsNullOrWhiteSpace(request.Gender)) record.Gender = request.Gender;
                    if (request.Relationship != null) record.Relationship = EmptyToNull(request.Relationship);
                    if (request.PersonalityTags != null) record.PersonalityTags = [.. request.PersonalityTags];
                    if (request.AppearanceTags != null) record.AppearanceTags = [.. request.AppearanceTags];
                    if (request.Height.HasValue) record.Height = request.Height;
                    if (request.Weight.HasValue) record.Weight = request.Weight;
                    if (request.Occupation != null) record.Occupation = EmptyToNull(request.Occupation.Trim());
                    if (request.School != null) record.School = EmptyToNull(request.School.Trim());
                    if (request.BloodType != null) record.BloodType = EmptyToNull(request.BloodType);
                    if (request.DatingGoal != null) record.DatingGoal = EmptyToNull(request.DatingGoal);
                    if (request.LookingFor != null) record.LookingFor = EmptyToNull(request.LookingFor);
                    if (request.AgeMin.HasValue) record.AgeMin = request.AgeMin;
                    if (request.AgeMax.HasValue) record.AgeMax = request.AgeMax;
                    if (request.DistanceKm.HasValue) record.DistanceKm = request.DistanceKm;
                    if (request.Languages != null) record.Languages = [.. request.Languages];
                    if (request.ActiveTime != null) record.ActiveTime = EmptyToNull(request.ActiveTime);
                    if (request.VoiceFirst.HasValue) record.VoiceFirst = request.VoiceFirst.Value;
                    if (request.MeetSoon.HasValue) record.MeetSoon = request.MeetSoon.Value;
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
            if (!string.IsNullOrWhiteSpace(request.Gender)) target.Gender = request.Gender;
            if (request.Relationship != null) target.Relationship = EmptyToNull(request.Relationship);
            if (request.PersonalityTags != null) target.PersonalityTags = [.. request.PersonalityTags];
            if (request.AppearanceTags != null) target.AppearanceTags = [.. request.AppearanceTags];
            if (request.Height.HasValue) target.Height = request.Height;
            if (request.Weight.HasValue) target.Weight = request.Weight;
            if (request.Occupation != null) target.Occupation = EmptyToNull(request.Occupation.Trim());
            if (request.School != null) target.School = EmptyToNull(request.School.Trim());
            if (request.BloodType != null) target.BloodType = EmptyToNull(request.BloodType);
            if (request.DatingGoal != null) target.DatingGoal = EmptyToNull(request.DatingGoal);
            if (request.LookingFor != null) target.LookingFor = EmptyToNull(request.LookingFor);
            if (request.AgeMin.HasValue) target.AgeMin = request.AgeMin;
            if (request.AgeMax.HasValue) target.AgeMax = request.AgeMax;
            if (request.DistanceKm.HasValue) target.DistanceKm = request.DistanceKm;
            if (request.Languages != null) target.Languages = [.. request.Languages];
            if (request.ActiveTime != null) target.ActiveTime = EmptyToNull(request.ActiveTime);
            if (request.VoiceFirst.HasValue) target.VoiceFirst = request.VoiceFirst.Value;
            if (request.MeetSoon.HasValue) target.MeetSoon = request.MeetSoon.Value;
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

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record UpdateProfileRequest(
    string? Nickname, string? Bio, List<string>? InterestTags, List<string>? ValueTags,
    List<string>? PhotoDataUrls = null, DateOnly? Birthday = null,
    string? Gender = null, string? Relationship = null,
    List<string>? PersonalityTags = null, List<string>? AppearanceTags = null,
    int? Height = null, int? Weight = null,
    string? Occupation = null, string? School = null, string? BloodType = null,
    string? DatingGoal = null, string? LookingFor = null,
    int? AgeMin = null, int? AgeMax = null, int? DistanceKm = null,
    List<string>? Languages = null, string? ActiveTime = null,
    bool? VoiceFirst = null, bool? MeetSoon = null);
