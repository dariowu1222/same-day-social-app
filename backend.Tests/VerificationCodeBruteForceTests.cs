using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 驗證碼猜錯幾次就該作廢，以及同一個信箱多久能再收到一封信。
///
/// 原本的樣子：驗證碼是 6 位數、有效 10 分鐘，唯一的防線是 AuthController 的
/// 每 IP 每分鐘 10 次。一個 IP 猜不完一百萬組，但一千個 IP 在十分鐘內可以猜掉
/// 四十萬組左右——大約四成的機率直接重設掉別人的密碼。
///
/// 重設密碼還有另一半：申請新碼時從來不作廢舊的（註冊那邊有做），所以可以先
/// 申請五次讓五組碼同時有效，猜中的機率再乘五。
/// </summary>
public sealed class VerificationCodeBruteForceTests
{
    private const string Email = "someone@example.com";
    private const string RightCode = "123456";
    private const string WrongCode = "999999";

    // ── 註冊 ──

    [Fact]
    public async Task TheRightCodeStillWorks()
    {
        var (service, db) = Build(nameof(TheRightCodeStillWorks));

        var result = await service.ConfirmRegistrationAsync(
            new ConfirmRegistrationCommand(Email, RightCode), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GuessingWrongCountsAgainstThatCode()
    {
        var (service, db) = Build(nameof(GuessingWrongCountsAgainstThatCode));

        await service.ConfirmRegistrationAsync(new ConfirmRegistrationCommand(Email, WrongCode), CancellationToken.None);

        Assert.Equal(1, db.RegistrationVerificationTokens.Single().FailedAttempts);
    }

    /// <summary>猜滿上限之後，連真正的那組碼都不該再管用。</summary>
    [Fact]
    public async Task AfterTooManyGuessesEvenTheRightCodeIsDead()
    {
        var (service, db) = Build(nameof(AfterTooManyGuessesEvenTheRightCodeIsDead));

        for (var i = 0; i < AuthSecurity.MaxCodeAttempts; i++)
        {
            await service.ConfirmRegistrationAsync(new ConfirmRegistrationCommand(Email, WrongCode), CancellationToken.None);
        }

        var result = await service.ConfirmRegistrationAsync(
            new ConfirmRegistrationCommand(Email, RightCode), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_CODE", result.Code);
        Assert.NotNull(db.RegistrationVerificationTokens.Single().UsedAt);
    }

    /// <summary>界線本身要還活著，不然上面那條用「永遠作廢」也會綠。</summary>
    [Fact]
    public async Task OneGuessShortOfTheLimitTheCodeStillWorks()
    {
        var (service, _) = Build(nameof(OneGuessShortOfTheLimitTheCodeStillWorks));

        for (var i = 0; i < AuthSecurity.MaxCodeAttempts - 1; i++)
        {
            await service.ConfirmRegistrationAsync(new ConfirmRegistrationCommand(Email, WrongCode), CancellationToken.None);
        }

        Assert.True((await service.ConfirmRegistrationAsync(
            new ConfirmRegistrationCommand(Email, RightCode), CancellationToken.None)).Success);
    }

    // ── 重設密碼 ──

    [Fact]
    public async Task GuessingAResetCodeWrongCountsToo()
    {
        var (service, db) = BuildReset(nameof(GuessingAResetCodeWrongCountsToo));

        await service.VerifyPasswordResetCodeAsync(Email, WrongCode, CancellationToken.None);

        Assert.Equal(1, db.PasswordResetTokens.Single().FailedAttempts);
    }

    [Fact]
    public async Task AfterTooManyGuessesTheResetCodeIsDead()
    {
        var (service, db) = BuildReset(nameof(AfterTooManyGuessesTheResetCodeIsDead));

        for (var i = 0; i < AuthSecurity.MaxCodeAttempts; i++)
        {
            await service.VerifyPasswordResetCodeAsync(Email, WrongCode, CancellationToken.None);
        }

        var result = await service.VerifyPasswordResetCodeAsync(Email, RightCode, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(db.PasswordResetTokens.Single().UsedAt);
    }

    /// <summary>
    /// 申請新碼要把舊的作廢。不作廢的話，先申請五次就有五組碼同時有效，
    /// 猜中的機率乘五——而嘗試次數是記在「每一組碼」身上的，所以多一組碼
    /// 就是多五次機會。
    /// </summary>
    [Fact]
    public async Task AskingForANewResetCodeKillsTheOldOnes()
    {
        var (service, db) = BuildReset(nameof(AskingForANewResetCodeKillsTheOldOnes));
        var firstId = db.PasswordResetTokens.Single().Id;

        await service.RequestPasswordResetAsync(Email, CancellationToken.None);

        Assert.NotNull(db.PasswordResetTokens.Single(x => x.Id == firstId).UsedAt);
        Assert.Single(db.PasswordResetTokens.Where(x => x.UsedAt == null));
    }

    // ── 寄信冷卻 ──

    [Fact]
    public async Task TwoResetRequestsInARowAreThrottled()
    {
        var (service, _) = BuildReset(nameof(TwoResetRequestsInARowAreThrottled));

        Assert.True((await service.RequestPasswordResetAsync(Email, CancellationToken.None)).Success);
        var second = await service.RequestPasswordResetAsync(Email, CancellationToken.None);

        Assert.False(second.Success);
        Assert.Equal("EMAIL_COOLDOWN", second.Code);
    }

    /// <summary>
    /// 查無此帳號的那條路照樣消耗額度。不然冷卻本身就成了帳號存在與否的
    /// 判斷方式——同一個信箱連打兩次，會被擋下來的就是註冊過的。
    /// </summary>
    [Fact]
    public async Task AnUnknownEmailIsThrottledTheSameWay()
    {
        var (service, _) = BuildReset(nameof(AnUnknownEmailIsThrottledTheSameWay));

        var first = await service.RequestPasswordResetAsync("nobody@example.com", CancellationToken.None);
        var second = await service.RequestPasswordResetAsync("nobody@example.com", CancellationToken.None);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal("EMAIL_COOLDOWN", second.Code);
    }

    // ── 測試資料 ──

    private static (AuthService Service, AppDbContext Db) Build(string databaseName)
    {
        var db = TestWorld.Db(databaseName);
        db.RegistrationVerificationTokens.Add(new RegistrationVerificationTokenRecord
        {
            Id = "reg_under_test",
            Email = Email,
            Nickname = "阿佑",
            PasswordHash = AuthSecurity.HashPassword("hunter2000"),
            CodeHash = AuthSecurity.HashVerificationCode(RightCode),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.SaveChanges();
        return (Service(db), db);
    }

    private static (AuthService Service, AppDbContext Db) BuildReset(string databaseName)
    {
        var db = TestWorld.Db(databaseName);
        var user = TestWorld.User("user_someone");
        db.Users.Add(user);
        db.AuthAccounts.Add(new AuthAccountRecord
        {
            Id = "auth_someone",
            UserId = user.Id,
            Username = Email,
            PasswordHash = AuthSecurity.HashPassword("hunter2000"),
        });
        db.PasswordResetTokens.Add(new PasswordResetTokenRecord
        {
            Id = "reset_under_test",
            UserId = user.Id,
            CodeHash = AuthSecurity.HashVerificationCode(RightCode),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.SaveChanges();
        return (Service(db), db);
    }

    private static AuthService Service(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(new EmailSendThrottle());

        return new AuthService(TestWorld.Storage(), new SilentEmailSender(), services.BuildServiceProvider());
    }

    /// <summary>寄信在這組測試裡沒有要測，收下就算。</summary>
    private sealed class SilentEmailSender : IEmailSender
    {
        public Task SendRegistrationVerificationCodeAsync(string toEmail, string code, CancellationToken ct)
            => Task.CompletedTask;

        public Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken ct)
            => Task.CompletedTask;

        public Task SendAdminLoginCodeAsync(string toEmail, string code, CancellationToken ct)
            => Task.CompletedTask;

        public Task SendAdminReverifyCodeAsync(string toEmail, string code, CancellationToken ct)
            => Task.CompletedTask;
    }
}
