using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Controllers;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// GET /api/profile/{userId} 給別人看到的是什麼。
///
/// 這支以前回整包 User，任何登入者拿一個 user id 就查得到：後台寫的處置原因
/// （這個人被停權過、為什麼）、最後活動時間（等於可以盯著任何一個人何時在線）、
/// 會員等級與到期日、帳號建立時間，以及這個人自己的交友條件——那是他拿來
/// 篩別人的設定，不是要給人看的。
///
/// 另一半是「公開個人檔案」那個開關。user_settings.profile_public 一直都在，
/// 但沒有任何一支讀取端點看它，所以使用者以為自己關掉了，實際上誰都查得到。
/// 設定頁上寫的是「關閉後僅配對成功的人能看到」。
/// </summary>
public sealed class ProfileVisibilityTests
{
    private const string Me = "user_me";
    private const string Peer = "user_peer";
    private const string Stranger = "user_stranger";

    // ── 自己看自己 ──

    [Fact]
    public async Task MyOwnProfileStillComesBackWhole()
    {
        var me = await Get(nameof(MyOwnProfileStillComesBackWhole), caller: Me, target: Me);

        Assert.Equal("偷偷記下的處置原因", me.StatusReason);
        Assert.Equal(25, me.AgeMin);
    }

    // ── 別人看我 ──

    [Fact]
    public async Task SomebodyElseDoesNotSeeTheSuspensionReason()
    {
        var seen = await Get(nameof(SomebodyElseDoesNotSeeTheSuspensionReason), caller: Stranger, target: Me);

        Assert.Null(seen.StatusReason);
    }

    [Fact]
    public async Task SomebodyElseDoesNotSeeWhenIWasLastOnline()
    {
        var seen = await Get(nameof(SomebodyElseDoesNotSeeWhenIWasLastOnline), caller: Stranger, target: Me);

        Assert.Null(seen.LastActiveAt);
    }

    /// <summary>年齡區間與距離是「我拿來篩別人」的條件，不是我的簡介。</summary>
    [Fact]
    public async Task SomebodyElseDoesNotSeeMySearchCriteria()
    {
        var seen = await Get(nameof(SomebodyElseDoesNotSeeMySearchCriteria), caller: Stranger, target: Me);

        Assert.Null(seen.AgeMin);
        Assert.Null(seen.AgeMax);
        Assert.Null(seen.DistanceKm);
    }

    /// <summary>
    /// MembershipTier 是 enum、預設 FREE，所以「沒帶出去」的樣子是停在預設值，
    /// 而不是 null——看不到真實的 PLUS 就對了。到期日是可空的，直接是 null。
    /// </summary>
    [Fact]
    public async Task SomebodyElseDoesNotSeeMyMembershipOrWhenItExpires()
    {
        var seen = await Get(nameof(SomebodyElseDoesNotSeeMyMembershipOrWhenItExpires), caller: Stranger, target: Me);

        Assert.NotEqual(MembershipTier.VIP, seen.MembershipTier);
        Assert.Null(seen.MembershipExpiresAt);
    }

    /// <summary>
    /// 看得到的還是要看得到：暱稱、照片、自我介紹、標籤——這些就是個人卡的內容。
    /// 生日也留著，聊天室的資料卡顯示的是年齡與星座，星座要月日才算得出來。
    /// </summary>
    [Fact]
    public async Task TheThingsAProfileCardActuallyShowsAreStillThere()
    {
        var seen = await Get(nameof(TheThingsAProfileCardActuallyShowsAreStillThere), caller: Stranger, target: Me);

        Assert.Equal("阿佑", seen.Nickname);
        Assert.Equal("今天開始慢慢認識同頻的人", seen.Bio);
        Assert.Equal(new DateOnly(1995, 3, 14), seen.Birthday);
        Assert.Equal(["看電影"], seen.InterestTags);
    }

    // ── 「公開個人檔案」關掉之後 ──

    [Fact]
    public async Task WithTheProfileSwitchedOffAStrangerGetsNothing()
    {
        var result = await GetRaw(
            nameof(WithTheProfileSwitchedOffAStrangerGetsNothing), caller: Stranger, target: Me, profilePublic: false);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    /// <summary>設定頁寫的是「關閉後僅配對成功的人能看到」。配對成功＝同在一間聊天室。</summary>
    [Fact]
    public async Task WithTheProfileSwitchedOffSomebodyIHaveMatchedWithStillSeesIt()
    {
        var seen = await Get(
            nameof(WithTheProfileSwitchedOffSomebodyIHaveMatchedWithStillSeesIt),
            caller: Peer, target: Me, profilePublic: false);

        Assert.Equal("阿佑", seen.Nickname);
    }

    [Fact]
    public async Task WithTheProfileSwitchedOffICanStillSeeMyself()
    {
        var seen = await Get(
            nameof(WithTheProfileSwitchedOffICanStillSeeMyself), caller: Me, target: Me, profilePublic: false);

        Assert.Equal("偷偷記下的處置原因", seen.StatusReason);
    }

    // ── 測試資料 ──

    private static async Task<User> Get(string databaseName, string caller, string target, bool profilePublic = true)
    {
        var result = await GetRaw(databaseName, caller, target, profilePublic);
        var response = Assert.IsType<ApiResponse<User>>(result.Value);
        Assert.NotNull(response.Data);
        return response.Data!;
    }

    private static Task<ActionResult<ApiResponse<User>>> GetRaw(
        string databaseName, string caller, string target, bool profilePublic = true)
    {
        var db = TestWorld.Db(databaseName);

        var me = TestWorld.User(Me);
        me.Nickname = "阿佑";
        me.Bio = "今天開始慢慢認識同頻的人";
        me.Birthday = new DateOnly(1995, 3, 14);
        me.InterestTags = ["看電影"];
        me.StatusReason = "偷偷記下的處置原因";
        me.LastActiveAt = DateTimeOffset.UtcNow;
        me.MembershipTier = nameof(MembershipTier.VIP);
        me.MembershipExpiresAt = DateTimeOffset.UtcNow.AddDays(30);
        me.AgeMin = 25;
        me.AgeMax = 35;
        me.DistanceKm = 10;

        db.Users.AddRange(me, TestWorld.User(Peer), TestWorld.User(Stranger));
        db.UserSettings.Add(new UserSettingRecord { UserId = Me, ProfilePublic = profilePublic });

        // Peer 是「配對成功」的那個人：同在一間聊天室
        db.ChatRoomUsers.AddRange(
            new ChatRoomUserRecord { Id = "member_me", ChatRoomId = "chat_ours", UserId = Me },
            new ChatRoomUserRecord { Id = "member_peer", ChatRoomId = "chat_ours", UserId = Peer });
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);

        var controller = new ProfileController(TestWorld.Storage(), TestWorld.MediaUrls(), services.BuildServiceProvider())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, caller)], "test")),
                },
            },
        };

        return controller.Get(target, CancellationToken.None);
    }
}
