using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Persistence;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 不走社群登入的那條路，從頭走到底：註冊 → 收驗證碼 → 驗證 → 登入。
///
/// 為什麼需要這組：原本的測試都是把 token 直接塞進資料庫再測某一段
/// （VerificationCodeBruteForceTests 就是這樣），沒有一條測試真的從
/// RegisterAsync 開始跑完整條。中間任何一段接不起來——驗證碼算出來跟
/// 驗的不是同一個、建帳號時密碼雜湊沒帶過去、帳號建了卻登不進去——
/// 現有的測試都照樣全綠。
///
/// 驗證碼只在寄信那一刻存在（資料庫裡存的是雜湊），所以這裡用一個會把
/// 驗證碼記下來的 IEmailSender 把它接住，等同於使用者去收信。
/// </summary>
public sealed class RegistrationToLoginFlowTests
{
    private const string Email = "newcomer@example.com";
    private const string Password = "tongpin2026";
    private const string Nickname = "新來的";

    /// <summary>整條路跑完，而且跑完之後真的能登入。</summary>
    [Fact]
    public async Task ARealPersonCanRegisterThenLogIn()
    {
        var (service, mail, _) = Build(nameof(ARealPersonCanRegisterThenLogIn));

        // 1. 送出註冊表單
        var started = await service.RegisterAsync(NewRegistration(), CancellationToken.None);
        Assert.True(started.Success, $"註冊被擋下：{started.Code} / {started.Message}");
        Assert.Equal(Email, started.Data!.Email);

        // 2. 信真的寄出去了，而且寄到填的那個信箱
        Assert.Equal(Email, mail.LastRegistrationTo);
        var code = mail.LastRegistrationCode;
        Assert.False(string.IsNullOrWhiteSpace(code), "沒有寄出驗證碼");

        // 3. 輸入收到的驗證碼
        var confirmed = await service.ConfirmRegistrationAsync(
            new ConfirmRegistrationCommand(Email, code!), CancellationToken.None);
        Assert.True(confirmed.Success, $"驗證被擋下：{confirmed.Code} / {confirmed.Message}");
        Assert.Equal(Nickname, confirmed.Data!.Nickname);

        // 4. 用註冊時的密碼登入
        var loggedIn = await service.LoginAsync(new LoginCommand(Email, Password), CancellationToken.None);
        Assert.True(loggedIn.Success, $"登入被擋下：{loggedIn.Code} / {loggedIn.Message}");

        // 登入拿到的要是同一個人，不是另外建的一顆帳號
        Assert.Equal(confirmed.Data.UserId, loggedIn.Data!.UserId);
    }

    /// <summary>驗證碼是六位數字，而且每次都不一樣。</summary>
    [Fact]
    public async Task TheCodeLooksLikeACodeAndIsNotReused()
    {
        var (service, mail, _) = Build(nameof(TheCodeLooksLikeACodeAndIsNotReused));
        var seen = new HashSet<string>();

        for (var i = 0; i < 5; i++)
        {
            // 同一個信箱有 60 秒冷卻，所以每次換一個
            var email = $"someone{i}@example.com";
            await service.RegisterAsync(NewRegistration(email), CancellationToken.None);
            var code = mail.LastRegistrationCode!;

            Assert.Equal(6, code.Length);
            Assert.True(code.All(char.IsDigit), $"驗證碼不是純數字：{code}");
            seen.Add(code);
        }

        // 五次全部相同的話，碼就不是隨機產生的
        Assert.True(seen.Count > 1, "連續五次拿到同一組驗證碼");
    }

    /// <summary>沒驗證完就想登入，不能放行——帳號根本還沒建。</summary>
    [Fact]
    public async Task LoggingInBeforeConfirmingIsRefused()
    {
        var (service, _, _) = Build(nameof(LoggingInBeforeConfirmingIsRefused));

        await service.RegisterAsync(NewRegistration(), CancellationToken.None);

        var result = await service.LoginAsync(new LoginCommand(Email, Password), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_CREDENTIALS", result.Code);
    }

    /// <summary>驗證過了，密碼要跟著整條路帶到底，錯的密碼不能進來。</summary>
    [Fact]
    public async Task TheWrongPasswordStillCannotGetIn()
    {
        var (service, mail, _) = Build(nameof(TheWrongPasswordStillCannotGetIn));

        await service.RegisterAsync(NewRegistration(), CancellationToken.None);
        await service.ConfirmRegistrationAsync(
            new ConfirmRegistrationCommand(Email, mail.LastRegistrationCode!), CancellationToken.None);

        var result = await service.LoginAsync(
            new LoginCommand(Email, "wrongpass2026"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_CREDENTIALS", result.Code);
    }

    /// <summary>註冊完的信箱不能再註冊一次。</summary>
    [Fact]
    public async Task TheSameEmailCannotRegisterTwice()
    {
        var (service, mail, _) = Build(nameof(TheSameEmailCannotRegisterTwice));

        await service.RegisterAsync(NewRegistration(), CancellationToken.None);
        await service.ConfirmRegistrationAsync(
            new ConfirmRegistrationCommand(Email, mail.LastRegistrationCode!), CancellationToken.None);

        var again = await service.RegisterAsync(NewRegistration(), CancellationToken.None);

        Assert.False(again.Success);
        Assert.Equal("ACCOUNT_EXISTS", again.Code);
    }

    /// <summary>
    /// 大小寫與前後空白不該讓人進不去自己的帳號：用 Newcomer@Example.com
    /// 註冊，之後打成小寫一樣要登得進來。
    /// </summary>
    [Fact]
    public async Task EmailCasingDoesNotLockPeopleOut()
    {
        var (service, mail, _) = Build(nameof(EmailCasingDoesNotLockPeopleOut));

        await service.RegisterAsync(NewRegistration("  Newcomer@Example.COM  "), CancellationToken.None);
        await service.ConfirmRegistrationAsync(
            new ConfirmRegistrationCommand(Email, mail.LastRegistrationCode!), CancellationToken.None);

        var result = await service.LoginAsync(new LoginCommand(Email, Password), CancellationToken.None);

        Assert.True(result.Success, $"大小寫不同就登不進去：{result.Code}");
    }

    /// <summary>寄信失敗時不該留下一顆半成品帳號，而且要能馬上重試。</summary>
    [Fact]
    public async Task WhenTheMailFailsNothingIsLeftBehind()
    {
        var (service, mail, db) = Build(nameof(WhenTheMailFailsNothingIsLeftBehind));
        mail.FailNextSend = true;

        var result = await service.RegisterAsync(NewRegistration(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("EMAIL_SEND_FAILED", result.Code);
        Assert.Empty(db.AuthAccounts);

        // 沒寄出去的信不佔冷卻，使用者按第二次就該通
        mail.FailNextSend = false;
        var retry = await service.RegisterAsync(NewRegistration(), CancellationToken.None);
        Assert.True(retry.Success, $"重試被冷卻擋下：{retry.Code}");
    }

    private static RegisterCommand NewRegistration(string? email = null)
        => new(Nickname, email ?? Email, Password, Password, "1995", "other", TermsAccepted: true);

    private static (AuthService Service, RecordingEmailSender Mail, AppDbContext Db) Build(string databaseName)
    {
        var db = TestWorld.Db(databaseName);
        var mail = new RecordingEmailSender();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(new EmailSendThrottle());

        return (new AuthService(TestWorld.Storage(), mail, services.BuildServiceProvider()), mail, db);
    }

    /// <summary>
    /// 站在收件匣的位置：把寄出去的驗證碼留下來，測試才有辦法「收信」。
    /// FailNextSend 用來模擬 SMTP 不通。
    /// </summary>
    private sealed class RecordingEmailSender : IEmailSender
    {
        public string? LastRegistrationCode { get; private set; }
        public string? LastRegistrationTo { get; private set; }
        public bool FailNextSend { get; set; }

        public Task SendRegistrationVerificationCodeAsync(string toEmail, string code, CancellationToken ct)
        {
            if (FailNextSend)
            {
                throw new InvalidOperationException("測試模擬 SMTP 不通");
            }

            LastRegistrationTo = toEmail;
            LastRegistrationCode = code;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken ct)
            => Task.CompletedTask;

        public Task SendAdminLoginCodeAsync(string toEmail, string code, CancellationToken ct)
            => Task.CompletedTask;

        public Task SendAdminReverifyCodeAsync(string toEmail, string code, CancellationToken ct)
            => Task.CompletedTask;

        public Task SendReportOutcomeToReporterAsync(string toEmail, bool upheld, CancellationToken ct)
            => Task.CompletedTask;

        public Task SendReportOutcomeToReportedAsync(string toEmail, bool upheld, bool contentHidden, CancellationToken ct)
            => Task.CompletedTask;
    }
}
