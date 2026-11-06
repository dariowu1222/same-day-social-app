using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Controllers;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// GET /api/profile/{userId} 的帳號狀態過濾。
///
/// 這支回的是整包 User——自我介紹、全部照片、身高體重、職業學校、交友條件，
/// 而且任何登入者都讀得到。自助註銷只洗了暱稱、自我介紹與照片，其餘幾十個欄位
/// 原封不動；後台封鎖則什麼都沒洗。不擋的話，被處置的帳號只是不能發言而已，
/// 個人檔案還在站上給人翻。
///
/// 這裡不測「查自己」：非 ACTIVE 的帳號在 Program.cs 的中介軟體就被擋在 401，
/// 走得到這支的呼叫者必定是 ACTIVE。
/// </summary>
public sealed class ProfileEndpointAccountStatusTests
{
    private const string Target = "user_target";

    [Fact]
    public async Task ActiveProfileIsReturned()
    {
        var result = await Get(nameof(ActiveProfileIsReturned), TestWorld.User(Target));

        Assert.Equal(Target, Payload(result).Id);
    }

    [Fact]
    public async Task DeletedProfileIsNotFound()
    {
        var result = await Get(nameof(DeletedProfileIsNotFound), TestWorld.User(Target, AccountStatus.DELETED));

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task BannedProfileIsNotFound()
    {
        var result = await Get(nameof(BannedProfileIsNotFound), TestWorld.User(Target, AccountStatus.BANNED));

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    /// <summary>停權還沒到期：擋住。這是下一個測試的對照組。</summary>
    [Fact]
    public async Task RunningSuspensionIsNotFound()
    {
        var result = await Get(
            nameof(RunningSuspensionIsNotFound),
            TestWorld.User(Target, AccountStatus.SUSPENDED, TestWorld.StillSuspended));

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    /// <summary>停權到期：檔案自己回來，不必等後台有人去按。</summary>
    [Fact]
    public async Task ExpiredSuspensionIsVisibleAgain()
    {
        var result = await Get(
            nameof(ExpiredSuspensionIsVisibleAgain),
            TestWorld.User(Target, AccountStatus.SUSPENDED, TestWorld.SuspensionOver));

        Assert.Equal(Target, Payload(result).Id);
    }

    // ── 測試資料 ──

    private static User Payload(ActionResult<ApiResponse<User>> result)
    {
        var response = Assert.IsType<ApiResponse<User>>(result.Value);
        Assert.NotNull(response.Data);
        return response.Data!;
    }

    private static Task<ActionResult<ApiResponse<User>>> Get(string databaseName, UserRecord target)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.Add(target);
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);

        var controller = new ProfileController(TestWorld.Storage(), TestWorld.MediaUrls(), services.BuildServiceProvider());
        return controller.Get(Target, CancellationToken.None);
    }
}
