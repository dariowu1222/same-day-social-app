using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Controllers;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 既有帳號補問「同意條款」與「年齡確認」。
///
/// 以前用 Google／Facebook 按一下就建好的帳號，當時什麼都沒問。新的社群註冊已經
/// 補上門檻（SocialSignupConsentTests），但已經存在的帳號不會再走到註冊那條路，
/// 所以另外做一組「缺不缺」的查詢與「補上」的寫入，由新版 App 登入後自己問。
///
/// 這裡要釘住的有三件事：
/// 1. 誰會被問——只有真的缺的人。問到不該問的人，是每個人每次開 App 都被擋一次。
/// 2. 補的門檻跟註冊一樣——沒勾同意、未成年都不能過。
/// 3. 不擋任何事——查詢只回答缺不缺，報了未成年也只是不放行，不動帳號。
///    擋在登入上的話，還在用舊版 App 的人會被鎖在外面，而舊版沒有畫面讓他補。
/// </summary>
public sealed class RetroactiveConsentTests
{
    private const string Me = "user_legacy";
    private const string AdultYear = "1995";

    // ── 誰會被問 ──

    /// <summary>舊的社群登入建的帳號：沒有年次、沒有任何紀錄。</summary>
    [Fact]
    public async Task LegacySocialAccountIsAsked()
    {
        var (auth, _) = World(nameof(LegacySocialAccountIsAsked));

        Assert.True(await IsAsked(auth));
    }

    /// <summary>Email 註冊的帳號兩樣都有（年次在 users 上、驗證完的那一列在註冊紀錄表裡）。</summary>
    [Fact]
    public async Task EmailRegisteredAccountIsNotAsked()
    {
        var (auth, db) = World(nameof(EmailRegisteredAccountIsNotAsked), birthYear: AdultYear);
        db.RegistrationVerificationTokens.Add(Token(EmailOf(Me), usedAt: DateTimeOffset.UtcNow.AddDays(-30)));
        db.SaveChanges();

        Assert.False(await IsAsked(auth));
    }

    /// <summary>走新的社群註冊（已經在確認畫面同意過）建出來的帳號，登入後不該再被問一次。</summary>
    [Fact]
    public async Task AccountFromTheNewSocialSignupIsNotAsked()
    {
        var (auth, _) = World(nameof(AccountFromTheNewSocialSignupIsNotAsked));

        var created = await auth.GoogleLoginAsync(
            new GoogleLoginCommand("brand-new@example.com", "小安", TermsAccepted: true, BirthYear: AdultYear), CancellationToken.None);

        Assert.True(created.Success);
        Assert.False(await IsAsked(auth, created.Data!.UserId));
    }

    /// <summary>
    /// 同一個信箱上「沒驗證完」或「已作廢」的註冊，都不是這個帳號主人的同意。
    /// 那種列上的 TermsAcceptedAt 也有值——是有人拿這個 email 填過註冊表單。
    /// </summary>
    [Fact]
    public async Task UnfinishedOrVoidedRegistrationIsNotConsent()
    {
        // 年次空著（舊社群帳號），信箱上躺著一列作廢的註冊：仍然要問
        var (authA, dbA) = World(nameof(UnfinishedOrVoidedRegistrationIsNotConsent) + "a");
        dbA.RegistrationVerificationTokens.Add(Token(EmailOf(Me), usedAt: DateTimeOffset.UtcNow.AddDays(-3)));
        dbA.SaveChanges();
        Assert.True(await IsAsked(authA));

        // 有年次，但信箱上只有一列還沒結掉的註冊：仍然要問
        var (authB, dbB) = World(nameof(UnfinishedOrVoidedRegistrationIsNotConsent) + "b", birthYear: AdultYear);
        dbB.RegistrationVerificationTokens.Add(Token(EmailOf(Me), usedAt: null));
        dbB.SaveChanges();
        Assert.True(await IsAsked(authB));
    }

    /// <summary>別人的同意紀錄不算我的。</summary>
    [Fact]
    public async Task SomeoneElsesRecordDoesNotCount()
    {
        var (auth, db) = World(nameof(SomeoneElsesRecordDoesNotCount), birthYear: AdultYear);
        db.RegistrationVerificationTokens.Add(Token("someone-else@example.com", usedAt: DateTimeOffset.UtcNow));
        db.SaveChanges();

        Assert.True(await IsAsked(auth));
    }

    [Fact]
    public async Task UnknownAccountIsAnError()
    {
        var (auth, _) = World(nameof(UnknownAccountIsAnError));

        var status = await auth.GetConsentStatusAsync("user_nobody", CancellationToken.None);
        var give = await auth.RecordConsentAsync("user_nobody", true, AdultYear, CancellationToken.None);

        Assert.Equal("ACCOUNT_NOT_FOUND", status.Code);
        Assert.Equal("ACCOUNT_NOT_FOUND", give.Code);
    }

    // ── 補上 ──

    [Fact]
    public async Task GivingConsentRecordsItAndStopsTheAsking()
    {
        var (auth, db) = World(nameof(GivingConsentRecordsItAndStopsTheAsking));
        var before = DateTimeOffset.UtcNow;

        var result = await auth.RecordConsentAsync(Me, termsAccepted: true, birthYear: AdultYear, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.Data!.Required);
        Assert.Equal(AdultYear, db.Users.Single(x => x.Id == Me).AgeRange);

        var record = Assert.Single(db.RegistrationVerificationTokens);
        Assert.Equal(EmailOf(Me), record.Email);
        Assert.Equal(AdultYear, record.BirthYear);
        Assert.InRange(record.TermsAcceptedAt, before, DateTimeOffset.UtcNow);
        // 這一列跟驗證碼住同一張表，不能是一顆還能用的驗證碼
        Assert.NotNull(record.UsedAt);
        Assert.True(record.ExpiresAt <= DateTimeOffset.UtcNow);

        Assert.False(await IsAsked(auth));
    }

    [Fact]
    public async Task WithoutAgreeingNothingIsRecorded()
    {
        var (auth, db) = World(nameof(WithoutAgreeingNothingIsRecorded));

        var result = await auth.RecordConsentAsync(Me, termsAccepted: false, birthYear: AdultYear, CancellationToken.None);

        Assert.Equal("TERMS_NOT_ACCEPTED", result.Code);
        AssertUntouched(db);
        Assert.True(await IsAsked(auth));
    }

    /// <summary>
    /// 未成年、沒填、亂填：不放行，而且帳號原封不動。
    /// 這是自己填的數字，打錯一個鍵也會落在這裡；要不要處置一個確定未成年的帳號是審核的事，
    /// 不該由一個輸入框順手決定。
    /// </summary>
    [Theory]
    [InlineData("2012")]
    [InlineData("abcd")]
    [InlineData("")]
    [InlineData(null)]
    public async Task UnderageOrMissingBirthYearIsRefusedAndTheAccountIsLeftAlone(string? birthYear)
    {
        var (auth, db) = World(nameof(UnderageOrMissingBirthYearIsRefusedAndTheAccountIsLeftAlone) + (birthYear ?? "null"));

        var result = await auth.RecordConsentAsync(Me, termsAccepted: true, birthYear, CancellationToken.None);

        Assert.Equal("UNDERAGE", result.Code);
        AssertUntouched(db);
        Assert.Equal(AccountStatus.ACTIVE.ToString(), db.Users.Single(x => x.Id == Me).Status);
        Assert.False(db.AuthAccounts.Single(x => x.UserId == Me).IsDisabled);
    }

    /// <summary>連按兩次、或兩台裝置各問一次：只留一列，第一次同意的時間不被蓋掉。</summary>
    [Fact]
    public async Task GivingConsentTwiceWritesOnce()
    {
        var (auth, db) = World(nameof(GivingConsentTwiceWritesOnce));

        await auth.RecordConsentAsync(Me, true, AdultYear, CancellationToken.None);
        var firstAgreedAt = db.RegistrationVerificationTokens.Single().TermsAcceptedAt;
        var again = await auth.RecordConsentAsync(Me, true, "1980", CancellationToken.None);

        Assert.True(again.Success);
        var record = Assert.Single(db.RegistrationVerificationTokens);
        Assert.Equal(firstAgreedAt, record.TermsAcceptedAt);
        Assert.Equal(AdultYear, db.Users.Single(x => x.Id == Me).AgeRange);
    }

    /// <summary>原本就有年次的帳號維持原值——這支端點是補缺的，不是改生日的後門。</summary>
    [Fact]
    public async Task ExistingBirthYearIsKept()
    {
        var (auth, db) = World(nameof(ExistingBirthYearIsKept), birthYear: "1990");

        var result = await auth.RecordConsentAsync(Me, true, "1985", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("1990", db.Users.Single(x => x.Id == Me).AgeRange);
        Assert.Equal("1990", Assert.Single(db.RegistrationVerificationTokens).BirthYear);
    }

    // ── 端點 ──

    /// <summary>
    /// 兩支端點都只作用在呼叫者自己身上。請求裡沒有 userId 可以填，
    /// 這條測的是控制器真的拿登入者的身分去做，而不是別的什麼。
    /// </summary>
    [Fact]
    public async Task EndpointsActOnTheCallerOnly()
    {
        var (auth, db) = World(nameof(EndpointsActOnTheCallerOnly));
        var other = TestWorld.User("user_other");
        db.Users.Add(other);
        db.AuthAccounts.Add(TestWorld.Account(other, "Test-passw0rd"));
        db.SaveChanges();
        var controller = ControllerFor(auth, Me);

        var before = await controller.Consent(CancellationToken.None);
        Assert.True(before.Value!.Data!.Required);

        var given = await controller.GiveConsent(new GiveConsentRequest(TermsAccepted: true, BirthYear: AdultYear), CancellationToken.None);
        Assert.False(given.Value!.Data!.Required);

        // 補完之後再問一次：我不缺了，另一個人還缺。兩個人的答案不一樣，
        // 才分得出查詢端點查的是不是呼叫者本人。
        var after = await controller.Consent(CancellationToken.None);
        Assert.False(after.Value!.Data!.Required);

        Assert.Equal(EmailOf(Me), Assert.Single(db.RegistrationVerificationTokens).Email);
        Assert.Equal(AdultYear, db.Users.Single(x => x.Id == Me).AgeRange);
        Assert.Null(db.Users.Single(x => x.Id == "user_other").AgeRange);
        Assert.True(await IsAsked(auth, "user_other"));
    }

    [Fact]
    public async Task EndpointRefusalsAreBadRequestsWithACode()
    {
        var (auth, _) = World(nameof(EndpointRefusalsAreBadRequestsWithACode));
        var controller = ControllerFor(auth, Me);

        var refused = await controller.GiveConsent(new GiveConsentRequest(TermsAccepted: true, BirthYear: "2012"), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(refused.Result);
        Assert.Equal("UNDERAGE", Assert.IsType<ApiResponse<ConsentStatus>>(bad.Value).Code);
    }

    /// <summary>空的請求內容要落在「沒同意」，不是綁定失敗——預設值就是什麼都沒答應。</summary>
    [Fact]
    public void EmptyRequestBodyMeansNoConsent()
    {
        var request = JsonSerializer.Deserialize<GiveConsentRequest>("{}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.False(request.TermsAccepted);
        Assert.Null(request.BirthYear);
    }

    // ── 測試資料 ──

    private static string EmailOf(string userId) => $"{userId}@example.com";

    private static async Task<bool> IsAsked(AuthService auth, string userId = Me)
    {
        var status = await auth.GetConsentStatusAsync(userId, CancellationToken.None);
        Assert.True(status.Success);
        return status.Data!.Required;
    }

    private static void AssertUntouched(AppDbContext db)
    {
        Assert.Null(db.Users.Single(x => x.Id == Me).AgeRange);
        Assert.Empty(db.RegistrationVerificationTokens);
    }

    /// <summary>註冊紀錄表的一列。usedAt 有值 = 已結掉（完成或作廢），null = 還在等驗證碼。</summary>
    private static RegistrationVerificationTokenRecord Token(string email, DateTimeOffset? usedAt)
        => new()
        {
            Id = $"reg_{Guid.NewGuid():N}",
            Email = email,
            Nickname = "某人",
            PasswordHash = "x",
            CodeHash = "x",
            BirthYear = AdultYear,
            TermsAcceptedAt = DateTimeOffset.UtcNow.AddDays(-30),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            UsedAt = usedAt,
        };

    /// <summary>一個既有帳號。birthYear 不給就是舊社群登入建的那種：users 上沒有年次。</summary>
    private static (AuthService Auth, AppDbContext Db) World(string databaseName, string? birthYear = null)
    {
        var db = TestWorld.Db(databaseName);
        var user = TestWorld.User(Me);
        user.AgeRange = birthYear;
        db.Users.Add(user);
        db.AuthAccounts.Add(TestWorld.Account(user, "Test-passw0rd"));
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        return (new AuthService(TestWorld.Storage(), new SilentEmailSender(), services.BuildServiceProvider()), db);
    }

    private static AccountController ControllerFor(AuthService auth, string userId)
        => new(auth)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
                }
            }
        };

    private sealed class SilentEmailSender : IEmailSender
    {
        public Task SendRegistrationVerificationCodeAsync(string toEmail, string code, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendAdminLoginCodeAsync(string toEmail, string code, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendAdminReverifyCodeAsync(string toEmail, string code, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendReportOutcomeToReporterAsync(string toEmail, bool upheld, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendReportOutcomeToReportedAsync(string toEmail, bool upheld, bool contentHidden, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
