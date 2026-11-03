using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Controllers;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 停權期滿把資料庫那列寫回 ACTIVE，以及排程打的那支端點。
///
/// 放不放行已經不靠這條路了——登入與讀取都自己看 suspended_until。這裡釘的是
/// 另一半：users.status 不能長期跟事實分岔，因為後台的會員列表、狀態篩選與儀表板
/// 計數是直接查那個欄位的，審核的人只看得到那個數字。
///
/// 端點也一起測，不是多餘：排程唯一能走的就是它，密鑰那一關如果寫壞，
/// 整條線會安靜地什麼都不做——沒有人會收到錯誤，狀態就只是永遠不更新。
/// </summary>
public sealed class RestoreExpiredSuspensionsTests
{
    [Fact]
    public void ExpiredSuspensionGoesBackToActive()
    {
        var db = World(nameof(ExpiredSuspensionGoesBackToActive),
            TestWorld.User("user_served", AccountStatus.SUSPENDED, TestWorld.SuspensionOver));

        var restored = Service(db).RestoreExpiredSuspensions();

        Assert.Equal(1, restored);
        var user = db.Users.Single();
        Assert.Equal(AccountStatus.ACTIVE.ToString(), user.Status);
        Assert.Null(user.SuspendedUntil);
        // is_disabled 是 users.status 的鏡子，沒一起清的話後台看起來正常、
        // 直接查資料庫的人卻會看到一個「正常但不能登入」的帳號
        Assert.False(db.AuthAccounts.Single().IsDisabled);
    }

    [Fact]
    public void RunningSuspensionIsLeftAlone()
    {
        var db = World(nameof(RunningSuspensionIsLeftAlone),
            TestWorld.User("user_suspended", AccountStatus.SUSPENDED, TestWorld.StillSuspended));

        var restored = Service(db).RestoreExpiredSuspensions();

        Assert.Equal(0, restored);
        Assert.Equal(AccountStatus.SUSPENDED.ToString(), db.Users.Single().Status);
    }

    /// <summary>封鎖沒有期限，不能被這支順手解開。</summary>
    [Fact]
    public void BannedAccountIsNotRestored()
    {
        var db = World(nameof(BannedAccountIsNotRestored),
            TestWorld.User("user_banned", AccountStatus.BANNED, TestWorld.SuspensionOver));

        var restored = Service(db).RestoreExpiredSuspensions();

        Assert.Equal(0, restored);
        Assert.Equal(AccountStatus.BANNED.ToString(), db.Users.Single().Status);
        Assert.True(db.AuthAccounts.Single().IsDisabled);
    }

    /// <summary>沒有期限的停權要人工解除，時間過去不算數。</summary>
    [Fact]
    public void OpenEndedSuspensionIsNotRestored()
    {
        var db = World(nameof(OpenEndedSuspensionIsNotRestored),
            TestWorld.User("user_indefinite", AccountStatus.SUSPENDED));

        var restored = Service(db).RestoreExpiredSuspensions();

        Assert.Equal(0, restored);
        Assert.Equal(AccountStatus.SUSPENDED.ToString(), db.Users.Single().Status);
    }

    // ── 排程端點 ──

    [Fact]
    public void EndpointRestoresWithCorrectSecret()
    {
        var db = World(nameof(EndpointRestoresWithCorrectSecret),
            TestWorld.User("user_served", AccountStatus.SUSPENDED, TestWorld.SuspensionOver));

        var result = Endpoint(db, "s3cret").RestoreExpiredSuspensions("s3cret");

        var response = Assert.IsType<ApiResponse<RestoreExpiredSuspensionsResult>>(result.Value);
        Assert.Equal(1, response.Data!.Restored);
        Assert.Equal(AccountStatus.ACTIVE.ToString(), db.Users.Single().Status);
    }

    [Fact]
    public void EndpointRejectsWrongSecret()
    {
        var db = World(nameof(EndpointRejectsWrongSecret),
            TestWorld.User("user_served", AccountStatus.SUSPENDED, TestWorld.SuspensionOver));

        var result = Endpoint(db, "s3cret").RestoreExpiredSuspensions("nope");

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(AccountStatus.SUSPENDED.ToString(), db.Users.Single().Status);
    }

    /// <summary>沒設密鑰等於這支沒開，回 404 而不是讓誰都能打。</summary>
    [Fact]
    public void EndpointIsClosedWhenNoSecretConfigured()
    {
        var db = World(nameof(EndpointIsClosedWhenNoSecretConfigured),
            TestWorld.User("user_served", AccountStatus.SUSPENDED, TestWorld.SuspensionOver));

        var result = Endpoint(db, null).RestoreExpiredSuspensions(null);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal(AccountStatus.SUSPENDED.ToString(), db.Users.Single().Status);
    }

    // ── 測試資料 ──

    private static AppDbContext World(string databaseName, UserRecord user)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.Add(user);
        db.AuthAccounts.Add(TestWorld.Account(user, "Test-passw0rd"));
        db.SaveChanges();
        return db;
    }

    private static AdminUserService Service(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        return new AdminUserService(services.BuildServiceProvider());
    }

    private static MaintenanceController Endpoint(AppDbContext db, string? secret)
        => new(Service(db),
            Options.Create(new MaintenanceOptions { TriggerSecret = secret }),
            NullLogger<MaintenanceController>.Instance);
}
