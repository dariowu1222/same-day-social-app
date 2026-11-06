using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Application.Validation;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Media;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/profile")]
public sealed class ProfileController(JsonStorageService storage, MediaUrlPolicy mediaUrls, IServiceProvider services) : ControllerBase
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

        var tooLong = TooLongField(request);
        if (tooLong != null)
        {
            return BadRequest(ApiResponse<User>.Fail("FIELD_TOO_LONG", tooLong));
        }

        if (db != null)
        {
            try
            {
                var record = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
                if (record != null)
                {
                    if (!PhotosAreOurs(request.PhotoDataUrls, record.PhotoDataUrls))
                    {
                        return BadRequest(ApiResponse<User>.Fail("INVALID_PHOTO_URL", "照片必須是從這個 App 上傳的。"));
                    }
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

        if (!PhotosAreOurs(request.PhotoDataUrls, storage.FindById<User>("users", userId)?.PhotoDataUrls))
        {
            return BadRequest(ApiResponse<User>.Fail("INVALID_PHOTO_URL", "照片必須是從這個 App 上傳的。"));
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

    /// <summary>
    /// 照片網址必須是自家物件儲存發出來的。
    ///
    /// 不然任何人都能把頭像指向自己的伺服器：別人一看到配對卡或聊天列表，
    /// 瀏覽器就替他送出一次請求，IP、裝置、確切的上線時間全都有了。
    ///
    /// 例外是「這個人本來就有的那幾張」。個人資料頁是整包欄位一起送的，
    /// 不是只送改動的那幾個，所以舊資料裡那幾張 picsum 的假照片（種子資料直接
    /// 寫進資料庫的，沒有經過這支端點）會在每次存檔時原樣回來。擋掉的是
    /// 「新加一個外部網址」，不是「有外部網址的人從此不能改資料」。
    /// </summary>
    private bool PhotosAreOurs(List<string>? requested, IEnumerable<string>? existing)
    {
        if (requested == null) return true;
        var kept = existing?.ToHashSet() ?? [];
        return requested.All(url => mediaUrls.IsOwn(url) || kept.Contains(url));
    }

    /// <summary>
    /// 超長的欄位。有長度的那些（varchar(n)）原本會一路撞到 SaveChanges 才丟
    /// Npgsql 例外變成 500；text[] 的那幾個則根本沒有上限。
    /// 回 null 表示都在範圍內。
    /// </summary>
    private static string? TooLongField(UpdateProfileRequest request)
    {
        if (!InputLimits.WithinLength(request.Bio, 500)) return "自我介紹最多 500 字。";
        if (!InputLimits.WithinLength(request.Occupation, 60)) return "職業最多 60 字。";
        if (!InputLimits.WithinLength(request.School, 60)) return "學校最多 60 字。";
        if (!InputLimits.WithinLength(request.Gender, 40)) return "性別欄位太長。";
        if (!InputLimits.WithinLength(request.Relationship, 40)) return "感情狀態欄位太長。";
        if (!InputLimits.WithinLength(request.BloodType, 10)) return "血型欄位太長。";
        if (!InputLimits.WithinLength(request.DatingGoal, 40)) return "交友目標欄位太長。";
        if (!InputLimits.WithinLength(request.LookingFor, 40)) return "想找的對象欄位太長。";
        if (!InputLimits.WithinLength(request.PreferredArea, 40)) return "偏好地區欄位太長。";
        if (!InputLimits.WithinLength(request.ActiveTime, 40)) return "活躍時段欄位太長。";

        foreach (var tags in new[]
                 {
                     request.InterestTags, request.ValueTags,
                     request.PersonalityTags, request.AppearanceTags, request.Languages,
                 })
        {
            if (!InputLimits.WithinList(tags, InputLimits.MaxTagCount, InputLimits.MaxTagLength))
            {
                return $"標籤最多 {InputLimits.MaxTagCount} 個，每個最多 {InputLimits.MaxTagLength} 字。";
            }
        }

        return InputLimits.WithinList(request.PhotoDataUrls, InputLimits.MaxPhotos, MediaUrlPolicy.MaxUrlLength)
            ? null
            : $"照片最多 {InputLimits.MaxPhotos} 張。";
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
