using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Controllers;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Persistence;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// PUT /api/profile/{userId} 的暱稱驗證。
///
/// 為什麼改名這條路需要自己的規則：註冊有 2–20 字的後端檢查
/// （AuthService.ValidateRegister），改名以前一條都沒有，唯一的上限是前端輸入框的
/// maxLength=20。繞過前端就什麼都擋不住，而超過 80 字還會直接撞上
/// users.nickname 的 varchar(80)，變成一個 Npgsql 例外而不是一句錯誤訊息。
///
/// 放大效果讓這件事更要緊：樹洞顯示的名字現在是即時從 users 取的
/// （RantService.WithCurrentNames），所以暱稱改一次，就換掉這個人在公開牆上
/// 所有歷史貼文與回覆上顯示的名字。
/// </summary>
public sealed class ProfileNicknameValidationTests
{
    private const string Me = "user_me";

    [Fact]
    public async Task AReasonableNicknameGoesThrough()
    {
        const string name = nameof(AReasonableNicknameGoesThrough);
        var result = await Update(name, "阿佑吞球者");

        Assert.Equal("阿佑吞球者", Payload(result).Nickname);

        using var reloaded = TestWorld.Db(name);
        Assert.Equal("阿佑吞球者", reloaded.Users.Single(x => x.Id == Me).Nickname);
    }

    [Fact]
    public async Task OneCharacterIsRejected()
    {
        var result = await Update(nameof(OneCharacterIsRejected), "阿");

        AssertRejected(result);
    }

    [Fact]
    public async Task TwentyOneCharactersAreRejected()
    {
        var result = await Update(nameof(TwentyOneCharactersAreRejected), new string('阿', 21));

        AssertRejected(result);
    }

    /// <summary>剛好在界線上的兩個長度要過，不然上面兩條用「永遠拒絕」也會綠。</summary>
    [Fact]
    public async Task TheBoundariesThemselvesAreFine()
    {
        Assert.Equal("阿佑", Payload(await Update(nameof(TheBoundariesThemselvesAreFine) + "2", "阿佑")).Nickname);

        var twenty = new string('阿', 20);
        Assert.Equal(twenty, Payload(await Update(nameof(TheBoundariesThemselvesAreFine) + "20", twenty)).Nickname);
    }

    /// <summary>前後空白先去掉再量，不然「 阿 」會被當成三個字放行。</summary>
    [Fact]
    public async Task PaddingWithSpacesDoesNotGetYouPastTheMinimum()
    {
        var result = await Update(nameof(PaddingWithSpacesDoesNotGetYouPastTheMinimum), "  阿  ");

        AssertRejected(result);
    }

    /// <summary>
    /// 空白等於「這次不改暱稱」，是原本就有的行為（個人資料頁是整包欄位一起送的，
    /// 不是只送改動的那幾個），別修壞了。
    /// </summary>
    [Fact]
    public async Task BlankStillMeansLeaveItAlone()
    {
        const string name = nameof(BlankStillMeansLeaveItAlone);
        var result = await Update(name, "   ");

        Assert.Equal(Me, Payload(result).Nickname);

        using var reloaded = TestWorld.Db(name);
        Assert.Equal(Me, reloaded.Users.Single(x => x.Id == Me).Nickname);
    }

    [Fact]
    public async Task NotSendingTheFieldAtAllLeavesItAlone()
    {
        var result = await Update(nameof(NotSendingTheFieldAtAllLeavesItAlone), null);

        Assert.Equal(Me, Payload(result).Nickname);
    }

    // ── 測試資料 ──

    private static void AssertRejected(ActionResult<ApiResponse<User>> result)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<User>>(bad.Value);
        Assert.Equal("INVALID_NICKNAME", body.Code);
    }

    private static User Payload(ActionResult<ApiResponse<User>> result)
    {
        var response = Assert.IsType<ApiResponse<User>>(result.Value);
        Assert.NotNull(response.Data);
        return response.Data!;
    }

    private static Task<ActionResult<ApiResponse<User>>> Update(string databaseName, string? nickname)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.Add(TestWorld.User(Me));   // 初始暱稱就是 id
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);

        var controller = new ProfileController(TestWorld.Storage(), services.BuildServiceProvider())
        {
            // 改別人的資料會被 Forbid 擋掉，所以呼叫者要是本人
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Me)], "test"))
                }
            }
        };

        // 其餘欄位全部 null——「沒送就不要動」正是這支的語意
        return controller.Update(
            Me,
            new UpdateProfileRequest(nickname, null, null, null),
            CancellationToken.None);
    }
}
