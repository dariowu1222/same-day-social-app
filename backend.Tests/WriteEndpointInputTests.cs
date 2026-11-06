using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Controllers;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 寫入端點收不收這包東西。
///
/// 兩件事合在這裡測，因為它們是同一個入口的兩面：
///
/// 1. 外部網址。頭像、發文圖片、語音原本接受任意 https 網址，而這些內容會出現在
///    別人的畫面上——配對卡、聊天列表、公開的樹洞牆。網址指向攻擊者的伺服器，
///    就是在每個看過的人身上裝一個追蹤像素。
///
/// 2. 長度與數量。chat_messages.content 是沒有長度的 text，興趣標籤與照片網址都是
///    text[]，而全站請求上限原本是預設的 28.6 MB。一個帳號可以把 profile 撐到
///    幾十 MB，之後每次配對都要把整張 users 表載進記憶體。有長度的欄位則是另一種
///    壞法：超長會一路撞到 SaveChanges 丟 Npgsql 例外，使用者看到的是 500。
/// </summary>
public sealed class WriteEndpointInputTests
{
    private const string Me = "user_me";
    private const string Own = TestWorld.OwnMediaPrefix + "avatars/0123456789abcdef0123456789abcdef.jpg";
    private const string Theirs = "https://evil.example/pixel.png";

    // ── 個人檔案 ──

    [Fact]
    public async Task APhotoFromOurOwnStorageIsAccepted()
    {
        const string name = nameof(APhotoFromOurOwnStorageIsAccepted);
        var result = await UpdateProfile(name, photos: [Own]);

        Assert.Equal([Own], Payload(result).PhotoDataUrls);
    }

    [Theory]
    [InlineData(Theirs)]
    [InlineData("data:image/jpeg;base64,/9j/4AAQSkZJRg==")]
    public async Task APhotoPointingSomewhereElseIsRejected(string url)
    {
        const string name = nameof(APhotoPointingSomewhereElseIsRejected);
        var result = await UpdateProfile(name + url.Length, photos: [url]);

        AssertRejected<User>(result, "INVALID_PHOTO_URL");
    }

    /// <summary>
    /// 個人資料頁是整包欄位一起送的，所以本來就有外部網址的人（種子資料直接寫進
    /// 資料庫的那幾張 picsum 假照片）每次存檔都會把它原樣送回來。
    /// 要擋的是「新加一個外部網址」，不是「有外部網址的人從此不能改資料」。
    /// </summary>
    [Fact]
    public async Task APhotoThatIsAlreadyOnTheRecordStillGoesThrough()
    {
        const string name = nameof(APhotoThatIsAlreadyOnTheRecordStillGoesThrough);
        var db = SeedUser(name, existingPhotos: [Theirs]);

        var result = await Controller(db).Update(
            Me, Request(photos: [Theirs, Own]), CancellationToken.None);

        Assert.Equal([Theirs, Own], Payload(result).PhotoDataUrls);
    }

    [Fact]
    public async Task TooManyPhotosAreRejected()
    {
        var result = await UpdateProfile(
            nameof(TooManyPhotosAreRejected),
            photos: [.. Enumerable.Range(0, 7).Select(_ => Own)]);

        AssertRejected<User>(result, "FIELD_TOO_LONG");
    }

    [Fact]
    public async Task ABioLongerThanTheColumnIsRejectedAsFourHundred()
    {
        var result = await UpdateProfile(nameof(ABioLongerThanTheColumnIsRejectedAsFourHundred), bio: new string('阿', 501));

        AssertRejected<User>(result, "FIELD_TOO_LONG");
    }

    [Fact]
    public async Task TooManyTagsAreRejected()
    {
        var result = await UpdateProfile(
            nameof(TooManyTagsAreRejected),
            tags: [.. Enumerable.Range(0, 11).Select(i => $"標籤{i}")]);

        AssertRejected<User>(result, "FIELD_TOO_LONG");
    }

    [Fact]
    public async Task AnAbsurdlyLongTagIsRejected()
    {
        var result = await UpdateProfile(nameof(AnAbsurdlyLongTagIsRejected), tags: [new string('阿', 21)]);

        AssertRejected<User>(result, "FIELD_TOO_LONG");
    }

    /// <summary>界線本身要過，不然上面那幾條用「永遠拒絕」也會綠。</summary>
    [Fact]
    public async Task TheLimitsThemselvesAreFine()
    {
        var result = await UpdateProfile(
            nameof(TheLimitsThemselvesAreFine),
            bio: new string('阿', 500),
            tags: [.. Enumerable.Range(0, 10).Select(_ => new string('阿', 20))],
            photos: [.. Enumerable.Range(0, 6).Select(_ => Own)]);

        Assert.Equal(6, Payload(result).PhotoDataUrls.Count);
    }

    // ── 樹洞 ──

    [Fact]
    public void APostWithOurOwnImageIsAccepted()
    {
        var posted = Rants(nameof(APostWithOurOwnImageIsAccepted)).Create(
            new CreateRantRequest("今天很累", RantMode.JUST_SAYING, ImageUrls: [Own], ThumbUrls: [Own]));

        Assert.Equal([Own], Assert.IsType<ApiResponse<RantPost>>(posted.Value).Data!.ImageUrls);
    }

    [Theory]
    [InlineData("image")]
    [InlineData("thumb")]
    [InlineData("audio")]
    public void APostPointingAtSomebodyElsesServerIsRejected(string which)
    {
        var request = which switch
        {
            "image" => new CreateRantRequest("今天很累", RantMode.JUST_SAYING, ImageUrls: [Theirs]),
            "thumb" => new CreateRantRequest("今天很累", RantMode.JUST_SAYING, ImageUrls: [Own], ThumbUrls: [Theirs]),
            _ => new CreateRantRequest("今天很累", RantMode.JUST_SAYING, AudioUrl: "https://evil.example/beacon.mp3"),
        };

        AssertRejected(Rants(nameof(APostPointingAtSomebodyElsesServerIsRejected) + which).Create(request), "INVALID_MEDIA_URL");
    }

    /// <summary>單圖那條舊路徑（ImageUrl）也要擋，不然繞過去就好了。</summary>
    [Fact]
    public void TheLegacySingleImageFieldIsCheckedToo()
    {
        var posted = Rants(nameof(TheLegacySingleImageFieldIsCheckedToo)).Create(
            new CreateRantRequest("今天很累", RantMode.JUST_SAYING, ImageUrl: Theirs));

        AssertRejected(posted, "INVALID_MEDIA_URL");
    }

    [Fact]
    public void AReplyPointingAtSomebodyElsesServerIsRejected()
    {
        AssertRejected(
            Rants(nameof(AReplyPointingAtSomebodyElsesServerIsRejected))
                .Reply("rant_any", new CreateReplyRequest("我懂", ImageUrl: Theirs)),
            "INVALID_MEDIA_URL");
    }

    [Fact]
    public void APostLongerThanTheColumnIsRejectedAsFourHundred()
    {
        AssertRejected(
            Rants(nameof(APostLongerThanTheColumnIsRejectedAsFourHundred))
                .Create(new CreateRantRequest(new string('阿', 3001), RantMode.JUST_SAYING)),
            "FIELD_TOO_LONG");
    }

    [Fact]
    public void AReplyLongerThanTheColumnIsRejectedAsFourHundred()
    {
        AssertRejected(
            Rants(nameof(AReplyLongerThanTheColumnIsRejectedAsFourHundred))
                .Reply("rant_any", new CreateReplyRequest(new string('阿', 1001))),
            "FIELD_TOO_LONG");
    }

    [Fact]
    public void TooManyHashTagsAreRejected()
    {
        AssertRejected(
            Rants(nameof(TooManyHashTagsAreRejected)).Create(new CreateRantRequest(
                "今天很累", RantMode.JUST_SAYING, HashTags: [.. Enumerable.Range(0, 11).Select(i => $"tag{i}")])),
            "FIELD_TOO_LONG");
    }

    /// <summary>
    /// Content 宣告為不可為 null，但 JSON 送 null 照樣會進來，
    /// 而服務層第一件事就是 content.Trim()——以前這裡是一個 500。
    /// </summary>
    [Fact]
    public void ANullBodyContentDoesNotBlowUp()
    {
        var posted = Rants(nameof(ANullBodyContentDoesNotBlowUp))
            .Create(new CreateRantRequest(null!, RantMode.JUST_SAYING));

        Assert.Equal("", Assert.IsType<ApiResponse<RantPost>>(posted.Value).Data!.Content);
    }

    // ── 測試資料 ──

    private static void AssertRejected<T>(ActionResult<ApiResponse<T>> result, string code)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(code, Assert.IsType<ApiResponse<T>>(bad.Value).Code);
    }

    private static void AssertRejected(ActionResult<ApiResponse<RantPost>> result, string code)
        => AssertRejected<RantPost>(result, code);

    private static User Payload(ActionResult<ApiResponse<User>> result)
    {
        var response = Assert.IsType<ApiResponse<User>>(result.Value);
        Assert.NotNull(response.Data);
        return response.Data!;
    }

    private static UpdateProfileRequest Request(
        string? bio = null, List<string>? tags = null, List<string>? photos = null)
        => new(null, bio, tags, null, photos);

    private static AppDbContext SeedUser(string databaseName, List<string>? existingPhotos = null)
    {
        var db = TestWorld.Db(databaseName);
        var user = TestWorld.User(Me);
        user.PhotoDataUrls = [.. existingPhotos ?? []];
        db.Users.Add(user);
        db.SaveChanges();
        return db;
    }

    private static ProfileController Controller(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);

        return new ProfileController(TestWorld.Storage(), TestWorld.MediaUrls(), services.BuildServiceProvider())
        {
            ControllerContext = CallerIsMe(),
        };
    }

    private static Task<ActionResult<ApiResponse<User>>> UpdateProfile(
        string databaseName, string? bio = null, List<string>? tags = null, List<string>? photos = null)
        => Controller(SeedUser(databaseName)).Update(Me, Request(bio, tags, photos), CancellationToken.None);

    private static RantsController Rants(string databaseName)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.Add(TestWorld.User(Me));
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);

        var rantService = new RantService(
            TestWorld.Storage(),
            new TodayAnalyzerService(),
            new ModerationService(),
            services.BuildServiceProvider());

        return new RantsController(rantService, TestWorld.MediaUrls()) { ControllerContext = CallerIsMe() };
    }

    private static ControllerContext CallerIsMe() => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Me)], "test")),
        },
    };
}
