using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/profile")]
public sealed class ProfileController(JsonStorageService storage, IServiceProvider services) : ControllerBase
{
    private readonly AppDbContext? db = services.GetService<AppDbContext>();

    // 個人資料會顯示在配對 / 聊天中，故任何登入者皆可讀取（但需登入，避免匿名爬取列舉）。
    //
    // 「任何登入者皆可讀取」的另一面：這支回的是整包 User——自我介紹、全部照片、
    // 身高體重、職業學校、交友條件。帳號被封鎖或停權之後不擋，等於那個人只是
    // 不能發言，個人檔案還在站上給人翻。自助註銷更明顯：DeleteAccount 只洗了
    // users 那邊的暱稱、自我介紹與照片，其餘幾十個欄位原封不動留著。
    //
    // 停權的人期滿會自動回來（判斷走 AccountStateService，不是只看 users.status，
    // 那個欄位要等有人開後台才會被寫回 ACTIVE）。
    //
    // 查詢自己不用另外放行：非 ACTIVE 的帳號在 Program.cs 的中介軟體就被擋在
    // 401，根本到不了這裡，所以能走到這一行的呼叫者必定是 ACTIVE。
    [Authorize]
    [HttpGet("{userId}")]
    public async Task<ActionResult<ApiResponse<User>>> Get(string userId, CancellationToken cancellationToken)
    {
        if (db != null)
        {
            try
            {
                var record = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
                if (record != null)
                {
                    return AccountStateService.IsActive(record.Status, record.SuspendedUntil, DateTimeOffset.UtcNow)
                        ? ApiResponse<User>.Ok(record.ToDomain())
                        : NotFound(ApiResponse<User>.Fail("USER_NOT_FOUND", "找不到使用者。"));
                }
            }
            catch { /* DB 欄位尚未 migrate，fallback 到 JSON */ }
        }

        var user = storage.FindById<User>("users", userId);
        if (user != null && !AccountStateService.IsActive(user.Status, user.SuspendedUntil, DateTimeOffset.UtcNow))
        {
            user = null;
        }
        return user == null
            ? NotFound(ApiResponse<User>.Fail("USER_NOT_FOUND", "找不到使用者。"))
            : ApiResponse<User>.Ok(user);
    }

    // 掛速率限制（每人每分鐘 60 次，見 Program.cs 的 "write" policy）。
    // 這支以前是全開的，而暱稱改一次就會換掉這個人在樹洞所有歷史貼文上顯示的名字
    // （RantService.WithCurrentNames 讀的是 users.nickname），一支沒有節流的端點
    // 配上這種放大效果不合適。
    [Authorize]
    [EnableRateLimiting("write")]
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

        // 暱稱的長度規則跟註冊同一套（AuthService.ValidateRegister）。
        //
        // 這條以前不在，註冊有、改名沒有，而唯一的上限是前端輸入框的 maxLength=20
        // ——繞過前端就什麼都擋不住，超過 80 字還會直接撞上 users.nickname 的
        // varchar(80) 丟 Npgsql 例外。
        //
        // 空字串／純空白仍然當成「這次不改暱稱」，維持原本的行為；要擋的是
        // 「有送、但不合規」。現有 81 個帳號的暱稱都落在 2–17 字，沒有人會被這條鎖在外面。
        var nickname = request.Nickname?.Trim();
        if (nickname is { Length: > 0 } && nickname.Length is < 2 or > 20)
        {
            return BadRequest(ApiResponse<User>.Fail("INVALID_NICKNAME", "暱稱需為 2 到 20 個字。"));
        }

        if (db != null)
        {
            try
            {
                var record = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
                if (record != null)
                {
                    if (nickname is { Length: > 0 }) record.Nickname = nickname;
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
                    if (request.PreferredArea != null) record.PreferredArea = EmptyToNull(request.PreferredArea);
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
            target.Nickname = nickname is { Length: > 0 } ? nickname : target.Nickname;
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
            if (request.PreferredArea != null) target.PreferredArea = EmptyToNull(request.PreferredArea);
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
    string? PreferredArea = null,
    List<string>? Languages = null, string? ActiveTime = null,
    bool? VoiceFirst = null, bool? MeetSoon = null);
