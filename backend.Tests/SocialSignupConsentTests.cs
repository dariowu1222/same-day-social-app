using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Controllers;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Persistence;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 第一次用 Google／Facebook 登入（＝註冊）時的條款同意與年齡門檻。
///
/// 以前這條路是按一下就建帳號：沒有同意條款、沒有年齡限制，而 Email 註冊兩樣都要。
/// 隱私權政策寫的「僅提供年滿十八歲之使用者」對社群登入沒有效力——
/// 未滿 18 歲的人在 Email 註冊被擋下之後，改按 Google 就進來了。
///
/// 另一半要釘住的是「不能因此把既有使用者鎖在外面」：已上架的版本只送憑證、
/// 不送同意與年次。所以門檻只加在「建立新帳號」，登入既有帳號一律照舊。
///
/// Google 與 Facebook 走同一條 SocialLoginAsync，主要用 Google 測，
/// Facebook 另外釘一條確認它沒有漏接參數。
/// </summary>
public sealed class SocialSignupConsentTests
{
    private const string NewEmail = "newcomer@example.com";
    private const string AdultYear = "1995";

    // ── 建立新帳號：兩樣都要 ──

    /// <summary>已上架的版本就是這樣打的：只有憑證。新帳號不能再這樣建出來。</summary>
    [Fact]
    public async Task NewAccountWithoutConsentIsNotCreated()
    {
        var (auth, db) = World(nameof(NewAccountWithoutConsentIsNotCreated));

        var result = await auth.GoogleLoginAsync(new GoogleLoginCommand(NewEmail, "小安"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("SOCIAL_CONSENT_REQUIRED", result.Code);
        AssertNothingCreated(db);
    }

    /// <summary>填了年次但沒勾同意：不建。年齡對了不代表同意了。</summary>
    [Fact]
    public async Task BirthYearAloneIsNotConsent()
    {
        var (auth, db) = World(nameof(BirthYearAloneIsNotConsent));

        var result = await auth.GoogleLoginAsync(
            new GoogleLoginCommand(NewEmail, "小安", TermsAccepted: false, BirthYear: AdultYear), CancellationToken.None);

        Assert.Equal("SOCIAL_CONSENT_REQUIRED", result.Code);
        AssertNothingCreated(db);
    }

    /// <summary>勾了同意但沒給年次：不建。少了這一條，年齡門檻只要不填就繞過去了。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConsentWithoutBirthYearIsNotEnough(string? birthYear)
    {
        var (auth, db) = World(nameof(ConsentWithoutBirthYearIsNotEnough) + (birthYear?.Length.ToString() ?? "null"));

        var result = await auth.GoogleLoginAsync(
            new GoogleLoginCommand(NewEmail, "小安", TermsAccepted: true, BirthYear: birthYear), CancellationToken.None);

        Assert.Equal("SOCIAL_CONSENT_REQUIRED", result.Code);
        AssertNothingCreated(db);
    }

    /// <summary>
    /// 未滿 18 歲、或根本不是年份：不建，而且回的是 UNDERAGE 不是「請同意」——
    /// 前端收到 SOCIAL_CONSENT_REQUIRED 會把人帶回確認畫面再問一次，
    /// 年齡不符的人不該被請回去「再填一次試試」。
    /// </summary>
    [Theory]
    [InlineData("2012")]
    [InlineData("1899")]
    [InlineData("abcd")]
    public async Task UnderageOrNonsenseBirthYearIsRejected(string birthYear)
    {
        var (auth, db) = World(nameof(UnderageOrNonsenseBirthYearIsRejected) + birthYear);

        var result = await auth.GoogleLoginAsync(
            new GoogleLoginCommand(NewEmail, "小安", TermsAccepted: true, BirthYear: birthYear), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("UNDERAGE", result.Code);
        AssertNothingCreated(db);
    }

    /// <summary>今年剛好滿 18 歲的出生年次是界線，要放行；再晚一年就不行。</summary>
    [Fact]
    public async Task TheYearThatTurnsEighteenIsTheBoundary()
    {
        var justAdult = (DateTimeOffset.Now.Year - 18).ToString();
        var notYet = (DateTimeOffset.Now.Year - 17).ToString();

        var (authA, _) = World(nameof(TheYearThatTurnsEighteenIsTheBoundary) + "a");
        var (authB, _) = World(nameof(TheYearThatTurnsEighteenIsTheBoundary) + "b");

        var allowed = await authA.GoogleLoginAsync(new GoogleLoginCommand(NewEmail, "小安", true, justAdult), CancellationToken.None);
        var refused = await authB.GoogleLoginAsync(new GoogleLoginCommand(NewEmail, "小安", true, notYet), CancellationToken.None);

        Assert.True(allowed.Success);
        Assert.Equal("UNDERAGE", refused.Code);
    }

    // ── 兩樣都有：建帳號，並留下紀錄 ──

    [Fact]
    public async Task ConsentAndAdultBirthYearCreateTheAccount()
    {
        var (auth, db) = World(nameof(ConsentAndAdultBirthYearCreateTheAccount));
        var before = DateTimeOffset.UtcNow;

        var result = await auth.GoogleLoginAsync(
            new GoogleLoginCommand(NewEmail, "小安", TermsAccepted: true, BirthYear: AdultYear), CancellationToken.None);

        Assert.True(result.Success);
        var account = Assert.Single(db.AuthAccounts);
        Assert.Equal(NewEmail, account.Username);

        // 年次寫在 users 上，跟 Email 註冊同一欄
        var user = Assert.Single(db.Users);
        Assert.Equal(AdultYear, user.AgeRange);

        // 同意的時間留在註冊紀錄表，跟 Email 註冊同一張表
        var record = Assert.Single(db.RegistrationVerificationTokens);
        Assert.Equal(NewEmail, record.Email);
        Assert.Equal(AdultYear, record.BirthYear);
        Assert.InRange(record.TermsAcceptedAt, before, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 那一列紀錄不能被當成「還沒用掉的驗證碼」。
    /// 它跟 Email 註冊的驗證碼住在同一張表，如果是未使用、未過期的狀態，
    /// 就成了一顆躺在那裡等人猜的驗證碼。
    /// </summary>
    [Fact]
    public async Task TheConsentRecordIsNotAUsableVerificationCode()
    {
        var (auth, db) = World(nameof(TheConsentRecordIsNotAUsableVerificationCode));

        await auth.GoogleLoginAsync(new GoogleLoginCommand(NewEmail, "小安", true, AdultYear), CancellationToken.None);

        var record = Assert.Single(db.RegistrationVerificationTokens);
        Assert.NotNull(record.UsedAt);
        Assert.True(record.ExpiresAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task FacebookSignupFollowsTheSameRule()
    {
        var (auth, db) = World(nameof(FacebookSignupFollowsTheSameRule));

        var refused = await auth.FacebookLoginAsync(new FacebookLoginCommand(NewEmail, "小安"), CancellationToken.None);
        Assert.Equal("SOCIAL_CONSENT_REQUIRED", refused.Code);
        AssertNothingCreated(db);

        var underage = await auth.FacebookLoginAsync(new FacebookLoginCommand(NewEmail, "小安", true, "2012"), CancellationToken.None);
        Assert.Equal("UNDERAGE", underage.Code);
        AssertNothingCreated(db);

        var created = await auth.FacebookLoginAsync(new FacebookLoginCommand(NewEmail, "小安", true, AdultYear), CancellationToken.None);
        Assert.True(created.Success);
        Assert.Equal(AdultYear, Assert.Single(db.Users).AgeRange);
    }

    // ── 登入既有帳號：照舊 ──

    /// <summary>
    /// 已上架的版本登入既有帳號：只有憑證，沒有同意、沒有年次。必須照樣進得來。
    /// 這條紅了，代表所有還沒更新 App 的人都被鎖在外面。
    /// </summary>
    [Fact]
    public async Task ExistingAccountLogsInWithoutConsentFields()
    {
        var (auth, db) = World(nameof(ExistingAccountLogsInWithoutConsentFields));
        var user = TestWorld.User("user_already_here");
        var account = TestWorld.Account(user, "Test-passw0rd");
        db.Users.Add(user);
        db.AuthAccounts.Add(account);
        db.SaveChanges();

        var result = await auth.GoogleLoginAsync(new GoogleLoginCommand(account.Username, "阿明"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(user.Id, result.Data!.UserId);
        // 登入不是註冊，不該多寫一列同意紀錄
        Assert.Empty(db.RegistrationVerificationTokens);
    }

    /// <summary>
    /// 既有帳號就算帶了一個未成年的年次進來，也是登入，不是重新審一次年齡。
    /// （前端只有在後端說要確認時才會送這兩欄，這條釘的是後端自己不要搞混兩條路。）
    /// </summary>
    [Fact]
    public async Task ExistingAccountIsNotReScreenedByBirthYear()
    {
        var (auth, db) = World(nameof(ExistingAccountIsNotReScreenedByBirthYear));
        var user = TestWorld.User("user_already_here");
        var account = TestWorld.Account(user, "Test-passw0rd");
        db.Users.Add(user);
        db.AuthAccounts.Add(account);
        db.SaveChanges();

        var result = await auth.GoogleLoginAsync(
            new GoogleLoginCommand(account.Username, "阿明", TermsAccepted: true, BirthYear: "2012"), CancellationToken.None);

        Assert.True(result.Success);
    }

    /// <summary>被封鎖的人不能靠「再同意一次」進來：帳號存在就走登入那條路，而登入會擋。</summary>
    [Fact]
    public async Task BannedAccountCannotComeBackThroughTheConsentPath()
    {
        var (auth, db) = World(nameof(BannedAccountCannotComeBackThroughTheConsentPath));
        var user = TestWorld.User("user_banned", AccountStatus.BANNED);
        var account = TestWorld.Account(user, "Test-passw0rd");
        db.Users.Add(user);
        db.AuthAccounts.Add(account);
        db.SaveChanges();

        var result = await auth.GoogleLoginAsync(
            new GoogleLoginCommand(account.Username, "阿明", TermsAccepted: true, BirthYear: AdultYear), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("ACCOUNT_DISABLED", result.Code);
        Assert.Single(db.AuthAccounts);
    }

    // ── HTTP 請求的形狀 ──

    /// <summary>
    /// 已上架的版本送的 JSON 只有憑證一個欄位。兩個新欄位必須有預設值，
    /// 不然舊版的請求連綁定都過不了，會在還沒進到登入邏輯之前就變成 400。
    /// </summary>
    [Fact]
    public void OldClientsRequestBodyStillBinds()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var google = JsonSerializer.Deserialize<GoogleLoginHttpRequest>("""{"idToken":"abc"}""", web)!;
        Assert.Equal("abc", google.IdToken);
        Assert.False(google.TermsAccepted);
        Assert.Null(google.BirthYear);

        var facebook = JsonSerializer.Deserialize<FacebookLoginHttpRequest>("""{"accessToken":"xyz"}""", web)!;
        Assert.Equal("xyz", facebook.AccessToken);
        Assert.False(facebook.TermsAccepted);
        Assert.Null(facebook.BirthYear);

        var confirmed = JsonSerializer.Deserialize<GoogleLoginHttpRequest>(
            """{"idToken":"abc","termsAccepted":true,"birthYear":"1995"}""", web)!;
        Assert.True(confirmed.TermsAccepted);
        Assert.Equal("1995", confirmed.BirthYear);
    }

    /// <summary>
    /// 請求 → 指令：身分來自驗證過的憑證，同意與年次來自請求，四樣都要到。
    /// 少帶一樣，新使用者就會永遠被請回確認畫面。
    /// </summary>
    [Fact]
    public void RequestCarriesConsentIntoTheCommand()
    {
        var google = new GoogleLoginHttpRequest("token", TermsAccepted: true, BirthYear: AdultYear)
            .ToCommand(new GoogleUserInfo("verified@example.com", "驗證過的名字"));
        Assert.Equal(new GoogleLoginCommand("verified@example.com", "驗證過的名字", true, AdultYear), google);

        var facebook = new FacebookLoginHttpRequest("token", TermsAccepted: true, BirthYear: AdultYear)
            .ToCommand(new FacebookUserInfo("verified@example.com", "驗證過的名字"));
        Assert.Equal(new FacebookLoginCommand("verified@example.com", "驗證過的名字", true, AdultYear), facebook);
    }

    // ── 測試資料 ──

    private static void AssertNothingCreated(AppDbContext db)
    {
        Assert.Empty(db.Users);
        Assert.Empty(db.AuthAccounts);
        Assert.Empty(db.RegistrationVerificationTokens);
    }

    private static (AuthService Auth, AppDbContext Db) World(string databaseName)
    {
        var db = TestWorld.Db(databaseName);
        var services = new ServiceCollection();
        services.AddSingleton(db);
        return (new AuthService(TestWorld.Storage(), new SilentEmailSender(), services.BuildServiceProvider()), db);
    }

    /// <summary>社群註冊不寄任何信；這個替身只是為了湊齊 AuthService 的建構子。</summary>
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
