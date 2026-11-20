using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Controllers;
using SameDaySocialApp.Domain;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Persistence;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 出生年月日是必填，而且要真的寫進 users.birthday。
///
/// 起因是配對的年齡篩選：它比的是 users.birthday，對方沒有生日就直接排除。
/// 可是生日以前是選填——Email 註冊只問出生年次、社群登入什麼都不問、onboarding 也不問，
/// 只有「我的」頁有一格。結果是線上 7 個帳號只有 2 個有生日，而 6 個人身上有年齡範圍
/// （onboarding 會替人寫入預設的 18–40），所以真人之間幾乎互相看不到。
///
/// 這裡釘 Email 註冊這條路與規則本身；社群登入在 SocialSignupConsentTests，
/// 既有帳號的補問在 RetroactiveConsentTests。
/// </summary>
public sealed class BirthdayRequirementTests
{
    private const string Email = "newcomer@example.com";
    private const string Password = "Test-passw0rd";
    private const string AdultBirthday = "1995-03-21";

    // ── 規則本身 ──

    [Fact]
    public void ABirthdayIsARealDateInAReasonableRange()
    {
        Assert.Equal(new DateOnly(1995, 3, 21), AgeRules.ParseBirthday("1995-03-21"));
        Assert.Equal(new DateOnly(2000, 2, 29), AgeRules.ParseBirthday("2000-02-29"));   // 閏年
        Assert.Equal(new DateOnly(1995, 3, 21), AgeRules.ParseBirthday("  1995-03-21 "));

        Assert.Null(AgeRules.ParseBirthday(null));
        Assert.Null(AgeRules.ParseBirthday(""));
        Assert.Null(AgeRules.ParseBirthday("1995"));          // 只有年次不是生日
        Assert.Null(AgeRules.ParseBirthday("1995/03/21"));    // 格式只收 yyyy-MM-dd
        Assert.Null(AgeRules.ParseBirthday("1995-3-21"));
        Assert.Null(AgeRules.ParseBirthday("1995-02-30"));    // 不存在的日期
        Assert.Null(AgeRules.ParseBirthday("2001-02-29"));    // 不是閏年
        Assert.Null(AgeRules.ParseBirthday("1939-12-31"));    // 早於下限：多半是填錯
        Assert.Null(AgeRules.ParseBirthday(AgeRules.Format(AgeRules.Today().AddDays(1))));   // 還沒到的日子
    }

    /// <summary>成年從滿 18 歲的當天起算；年齡在生日當天才加一歲。</summary>
    [Fact]
    public void AdulthoodAndAgeTurnOverOnTheBirthdayItself()
    {
        var today = AgeRules.Today();

        Assert.True(AgeRules.IsAdult(today.AddYears(-18)));
        Assert.False(AgeRules.IsAdult(today.AddYears(-18).AddDays(1)));
        Assert.True(AgeRules.IsAdult(today.AddYears(-40)));

        Assert.Equal(30, AgeRules.AgeOf(today.AddYears(-30)));
        Assert.Equal(29, AgeRules.AgeOf(today.AddYears(-30).AddDays(1)));
        Assert.Equal(30, AgeRules.AgeOf(today.AddYears(-31).AddDays(1)));
    }

    /// <summary>
    /// 「今天」是台灣的今天。主機跑在 UTC，台灣凌晨零點到八點之間兩邊差一天——
    /// 用主機的日期，剛滿 18 歲的人那幾個小時會被擋在外面。
    /// </summary>
    [Fact]
    public void TodayIsTaiwansToday()
    {
        // UTC 15:59 是台灣當天 23:59；UTC 16:00 已經是台灣隔天的零點
        Assert.Equal(new DateOnly(2026, 5, 10), AgeRules.DateInTaiwan(new DateTimeOffset(2026, 5, 10, 15, 59, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 5, 11), AgeRules.DateInTaiwan(new DateTimeOffset(2026, 5, 10, 16, 0, 0, TimeSpan.Zero)));
        // 不管傳進來的時刻本身帶什麼時區，答案都一樣
        Assert.Equal(new DateOnly(2026, 5, 11), AgeRules.DateInTaiwan(new DateTimeOffset(2026, 5, 10, 11, 0, 0, TimeSpan.FromHours(-5))));

        Assert.Equal(AgeRules.DateInTaiwan(DateTimeOffset.UtcNow), AgeRules.Today());
    }

    // ── Email 註冊 ──

    [Fact]
    public async Task RegisteringWithABirthdayStoresItOnTheUser()
    {
        var (auth, db, mail) = World(nameof(RegisteringWithABirthdayStoresItOnTheUser));

        var started = await auth.RegisterAsync(Command(birthday: AdultBirthday), CancellationToken.None);
        Assert.True(started.Success);

        var confirmed = await auth.ConfirmRegistrationAsync(new ConfirmRegistrationCommand(Email, mail.LastCode!), CancellationToken.None);
        Assert.True(confirmed.Success);

        var user = Assert.Single(db.Users);
        Assert.Equal(new DateOnly(1995, 3, 21), user.Birthday);
        Assert.Equal("1995", user.AgeRange);

        // 三樣都齊了，登入後不該再被補問
        var status = await auth.GetConsentStatusAsync(user.Id, CancellationToken.None);
        Assert.False(status.Data!.Required);
    }

    /// <summary>未滿 18 歲是照完整的日期算的：同一年出生，生日還沒到就還不行。</summary>
    [Fact]
    public async Task UnderageByTheExactDateCannotRegister()
    {
        var (auth, db, mail) = World(nameof(UnderageByTheExactDateCannotRegister));
        var turnsEighteenTomorrow = AgeRules.Format(AgeRules.Today().AddYears(-18).AddDays(1));

        var result = await auth.RegisterAsync(Command(birthday: turnsEighteenTomorrow), CancellationToken.None);

        Assert.Equal("UNDERAGE", result.Code);
        Assert.Empty(db.RegistrationVerificationTokens);
        Assert.Null(mail.LastCode);
    }

    [Fact]
    public async Task TurningEighteenTodayCanRegister()
    {
        var (auth, _, _) = World(nameof(TurningEighteenTodayCanRegister));
        var turnsEighteenToday = AgeRules.Format(AgeRules.Today().AddYears(-18));

        var result = await auth.RegisterAsync(Command(birthday: turnsEighteenToday), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Theory]
    [InlineData("1995-02-30")]
    [InlineData("1899-01-01")]
    [InlineData("not-a-date")]
    public async Task SomethingThatIsNotABirthdayCannotRegister(string birthday)
    {
        var (auth, db, _) = World(nameof(SomethingThatIsNotABirthdayCannotRegister) + birthday);

        var result = await auth.RegisterAsync(Command(birthday: birthday), CancellationToken.None);

        Assert.Equal("INVALID_BIRTHDAY", result.Code);
        Assert.Empty(db.RegistrationVerificationTokens);
    }

    /// <summary>
    /// 有給生日就以生日為準。年次那一格是給舊版 App 用的退路，
    /// 不能變成「生日填未成年、年次填成年」就過關的後門。
    /// </summary>
    [Fact]
    public async Task AnAdultBirthYearDoesNotRescueAnUnderageBirthday()
    {
        var (auth, db, _) = World(nameof(AnAdultBirthYearDoesNotRescueAnUnderageBirthday));
        var seventeen = AgeRules.Format(AgeRules.Today().AddYears(-17));

        var result = await auth.RegisterAsync(Command(birthday: seventeen, birthYear: "1990"), CancellationToken.None);

        Assert.Equal("UNDERAGE", result.Code);
        Assert.Empty(db.RegistrationVerificationTokens);
    }

    /// <summary>
    /// 已上架的舊版 App 只有「出生年次」一格，送不出日期。它的註冊必須照樣成立——
    /// 連 Email 註冊都要求日期的話，舊版上就完全沒有辦法建立帳號了。
    /// 這樣建出來的帳號沒有生日，等他更新之後由補問畫面補上。
    /// </summary>
    [Fact]
    public async Task OldClientsStillRegisterWithOnlyABirthYearAndAreAskedLater()
    {
        var (auth, db, mail) = World(nameof(OldClientsStillRegisterWithOnlyABirthYearAndAreAskedLater));

        var started = await auth.RegisterAsync(Command(birthday: null, birthYear: "1995"), CancellationToken.None);
        Assert.True(started.Success);
        await auth.ConfirmRegistrationAsync(new ConfirmRegistrationCommand(Email, mail.LastCode!), CancellationToken.None);

        var user = Assert.Single(db.Users);
        Assert.Null(user.Birthday);
        Assert.Equal("1995", user.AgeRange);

        var status = await auth.GetConsentStatusAsync(user.Id, CancellationToken.None);
        Assert.True(status.Data!.Required);
    }

    [Fact]
    public async Task OldClientsUnderageBirthYearIsStillRefused()
    {
        var (auth, _, _) = World(nameof(OldClientsUnderageBirthYearIsStillRefused));
        var tooYoung = (AgeRules.Today().Year - 17).ToString();

        var result = await auth.RegisterAsync(Command(birthday: null, birthYear: tooYoung), CancellationToken.None);

        Assert.Equal("UNDERAGE", result.Code);
    }

    // ── HTTP 請求的形狀 ──

    /// <summary>舊版送的註冊內容沒有 birthday 這一欄，仍然要綁得上；新版多帶的那一欄要讀得到。</summary>
    [Fact]
    public void RegisterRequestBindsWithAndWithoutABirthday()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        const string oldBody = """{"nickname":"小安","email":"a@example.com","password":"x","confirmPassword":"x","birthYear":"1995","gender":"PRIVATE","termsAccepted":true}""";
        const string newBody = """{"nickname":"小安","email":"a@example.com","password":"x","confirmPassword":"x","birthYear":"1995","birthday":"1995-03-21","gender":"PRIVATE","termsAccepted":true}""";

        var oldRequest = JsonSerializer.Deserialize<RegisterRequest>(oldBody, web)!;
        Assert.Equal("1995", oldRequest.BirthYear);
        Assert.Null(oldRequest.Birthday);

        var newRequest = JsonSerializer.Deserialize<RegisterRequest>(newBody, web)!;
        Assert.Equal(AdultBirthday, newRequest.Birthday);
    }

    /// <summary>
    /// 請求 → 指令：生日那一欄要真的被帶進去。
    /// 漏接的話不會有任何錯誤——新版同時也送年次，註冊照樣成功，只是建出來的帳號沒有生日，
    /// 「必填」就這樣安靜地失效了。
    /// </summary>
    [Fact]
    public void RegisterRequestCarriesTheBirthdayIntoTheCommand()
    {
        var command = new RegisterRequest("小安", Email, Password, Password, "1995", "PRIVATE", TermsAccepted: true, Birthday: AdultBirthday)
            .ToCommand();

        Assert.Equal(
            new RegisterCommand("小安", Email, Password, Password, "1995", "PRIVATE", TermsAccepted: true, Birthday: AdultBirthday),
            command);
    }

    // ── 測試資料 ──

    private static RegisterCommand Command(string? birthday, string birthYear = "")
        => new("小安", Email, Password, Password, birthYear, "PRIVATE", TermsAccepted: true, Birthday: birthday);

    private static (AuthService Auth, AppDbContext Db, CodeCatcher Mail) World(string databaseName)
    {
        var db = TestWorld.Db(databaseName);
        var services = new ServiceCollection();
        services.AddSingleton(db);
        var mail = new CodeCatcher();
        return (new AuthService(TestWorld.Storage(), mail, services.BuildServiceProvider()), db, mail);
    }

    /// <summary>
    /// 把寄出去的註冊驗證碼接住。資料庫裡只存驗證碼的雜湊，測試要走完「註冊→輸入驗證碼」
    /// 這一段，只能從寄信這一端拿到原文——跟真的使用者一樣。
    /// </summary>
    private sealed class CodeCatcher : IEmailSender
    {
        public string? LastCode { get; private set; }

        public Task SendRegistrationVerificationCodeAsync(string toEmail, string code, CancellationToken cancellationToken)
        {
            LastCode = code;
            return Task.CompletedTask;
        }

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
