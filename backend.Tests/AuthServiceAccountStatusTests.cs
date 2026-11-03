using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 登入時的帳號狀態判斷。
///
/// 這裡釘的是一個會悄悄發生的矛盾：處置時 auth_accounts.is_disabled 和 users.status
/// 一起寫下，但停權是有期限的，時間過去只有 suspended_until 的意義會變，is_disabled
/// 永遠停在當初那個值。以前登入只看 is_disabled，於是停權期滿的人手上那顆舊 token
/// 還能用（請求閘門自己會算期限），重新登入卻被擋在門外，而且畫面上只說「請聯絡客服」
/// ——等 token 一過期，他就徹底進不來了。
///
/// 所以每一組都刻意讓 is_disabled 留在處置當下的值（見 TestWorld.Account），
/// 答案必須完全由 users.status 與 suspended_until 決定。
///
/// 社群登入另外測一次，不是重複：它是另一條程式路徑（SocialLoginAsync），
/// 漏掉的話被封鎖的人只要改按 Google 就繞過去了。
/// </summary>
public sealed class AuthServiceAccountStatusTests
{
    private const string Password = "Test-passw0rd";

    // ── Email 密碼登入 ──

    [Fact]
    public async Task ActiveAccountCanLogIn()
    {
        var (auth, account) = World(nameof(ActiveAccountCanLogIn), TestWorld.User("user_active"));

        var result = await auth.LoginAsync(new LoginCommand(account.Username, Password), CancellationToken.None);

        Assert.True(result.Success);
    }

    /// <summary>停權還沒到期：擋住。這是下一個測試的對照組。</summary>
    [Fact]
    public async Task RunningSuspensionCannotLogIn()
    {
        var (auth, account) = World(
            nameof(RunningSuspensionCannotLogIn),
            TestWorld.User("user_suspended", AccountStatus.SUSPENDED, TestWorld.StillSuspended));

        var result = await auth.LoginAsync(new LoginCommand(account.Username, Password), CancellationToken.None);

        Assert.Equal("ACCOUNT_DISABLED", result.Code);
    }

    /// <summary>停權到期：人自己回得來，不必等排程或等管理員去按。</summary>
    [Fact]
    public async Task ExpiredSuspensionCanLogInAgain()
    {
        var (auth, account) = World(
            nameof(ExpiredSuspensionCanLogInAgain),
            TestWorld.User("user_served", AccountStatus.SUSPENDED, TestWorld.SuspensionOver));

        var result = await auth.LoginAsync(new LoginCommand(account.Username, Password), CancellationToken.None);

        Assert.True(result.Success);
    }

    /// <summary>沒有期限的停權不會因為時間過去而解除，要人工處理。</summary>
    [Fact]
    public async Task OpenEndedSuspensionCannotLogIn()
    {
        var (auth, account) = World(
            nameof(OpenEndedSuspensionCannotLogIn),
            TestWorld.User("user_indefinite", AccountStatus.SUSPENDED));

        var result = await auth.LoginAsync(new LoginCommand(account.Username, Password), CancellationToken.None);

        Assert.Equal("ACCOUNT_DISABLED", result.Code);
    }

    /// <summary>封鎖沒有期限，放行到期停權不能把這個洞一起開了。</summary>
    [Fact]
    public async Task BannedAccountCannotLogIn()
    {
        var (auth, account) = World(
            nameof(BannedAccountCannotLogIn),
            TestWorld.User("user_banned", AccountStatus.BANNED));

        var result = await auth.LoginAsync(new LoginCommand(account.Username, Password), CancellationToken.None);

        Assert.Equal("ACCOUNT_DISABLED", result.Code);
    }

    /// <summary>
    /// 封鎖時若有人順手填了 suspended_until（例如先停權再改判封鎖，欄位沒清掉），
    /// 那個過期的日期不能把封鎖解開——IsActive 只對 SUSPENDED 看期限。
    /// </summary>
    [Fact]
    public async Task BannedAccountWithStaleSuspensionDateCannotLogIn()
    {
        var (auth, account) = World(
            nameof(BannedAccountWithStaleSuspensionDateCannotLogIn),
            TestWorld.User("user_banned_stale", AccountStatus.BANNED, TestWorld.SuspensionOver));

        var result = await auth.LoginAsync(new LoginCommand(account.Username, Password), CancellationToken.None);

        Assert.Equal("ACCOUNT_DISABLED", result.Code);
    }

    [Fact]
    public async Task DeletedAccountCannotLogIn()
    {
        var (auth, account) = World(
            nameof(DeletedAccountCannotLogIn),
            TestWorld.User("user_deleted", AccountStatus.DELETED));

        var result = await auth.LoginAsync(new LoginCommand(account.Username, Password), CancellationToken.None);

        Assert.Equal("ACCOUNT_DISABLED", result.Code);
    }

    // ── 社群登入（Google / Facebook 共用同一條路徑）──

    [Fact]
    public async Task ExpiredSuspensionCanUseGoogleLogin()
    {
        var (auth, account) = World(
            nameof(ExpiredSuspensionCanUseGoogleLogin),
            TestWorld.User("user_social_served", AccountStatus.SUSPENDED, TestWorld.SuspensionOver));

        var result = await auth.GoogleLoginAsync(new GoogleLoginCommand(account.Username, "阿明"), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task RunningSuspensionCannotUseGoogleLogin()
    {
        var (auth, account) = World(
            nameof(RunningSuspensionCannotUseGoogleLogin),
            TestWorld.User("user_social_suspended", AccountStatus.SUSPENDED, TestWorld.StillSuspended));

        var result = await auth.GoogleLoginAsync(new GoogleLoginCommand(account.Username, "阿明"), CancellationToken.None);

        Assert.Equal("ACCOUNT_DISABLED", result.Code);
    }

    /// <summary>被封鎖的人不能改按 Google 就繞過去。</summary>
    [Fact]
    public async Task BannedAccountCannotUseGoogleLogin()
    {
        var (auth, account) = World(
            nameof(BannedAccountCannotUseGoogleLogin),
            TestWorld.User("user_social_banned", AccountStatus.BANNED));

        var result = await auth.GoogleLoginAsync(new GoogleLoginCommand(account.Username, "阿明"), CancellationToken.None);

        Assert.Equal("ACCOUNT_DISABLED", result.Code);
    }

    /// <summary>Facebook 走的是同一支 SocialLoginAsync，一起釘住免得哪天被拆開。</summary>
    [Fact]
    public async Task BannedAccountCannotUseFacebookLogin()
    {
        var (auth, account) = World(
            nameof(BannedAccountCannotUseFacebookLogin),
            TestWorld.User("user_fb_banned", AccountStatus.BANNED));

        var result = await auth.FacebookLoginAsync(new FacebookLoginCommand(account.Username, "阿明"), CancellationToken.None);

        Assert.Equal("ACCOUNT_DISABLED", result.Code);
    }

    /// <summary>
    /// 註銷的帳號不能靠社群登入「借屍還魂」。這條特別容易漏：SocialLoginAsync
    /// 找不到帳號就會建一個新的，所以擋的位置必須在既有帳號那一側。
    /// </summary>
    [Fact]
    public async Task DeletedAccountCannotUseGoogleLogin()
    {
        var (auth, account) = World(
            nameof(DeletedAccountCannotUseGoogleLogin),
            TestWorld.User("user_social_deleted", AccountStatus.DELETED));

        var result = await auth.GoogleLoginAsync(new GoogleLoginCommand(account.Username, "阿明"), CancellationToken.None);

        Assert.Equal("ACCOUNT_DISABLED", result.Code);
    }

    // ── 重設密碼：不能拿來繞過處置 ──

    /// <summary>
    /// 被封鎖的人不該收得到重設密碼的驗證碼。回應刻意跟成功一樣（去帳號枚舉），
    /// 所以這裡驗的是「沒有真的產生 token」。
    /// </summary>
    [Fact]
    public async Task BannedAccountGetsNoPasswordResetToken()
    {
        var (auth, account) = World(
            nameof(BannedAccountGetsNoPasswordResetToken),
            TestWorld.User("user_reset_banned", AccountStatus.BANNED),
            out var db);

        var result = await auth.RequestPasswordResetAsync(account.Username, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(db.PasswordResetTokens);
    }

    /// <summary>停權期滿的人要能自己走忘記密碼回來——很多人就是在停權期間忘了密碼。</summary>
    [Fact]
    public async Task ExpiredSuspensionGetsPasswordResetToken()
    {
        var (auth, account) = World(
            nameof(ExpiredSuspensionGetsPasswordResetToken),
            TestWorld.User("user_reset_served", AccountStatus.SUSPENDED, TestWorld.SuspensionOver),
            out var db);

        var result = await auth.RequestPasswordResetAsync(account.Username, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(db.PasswordResetTokens);
    }

    // ── 測試資料 ──

    private static (AuthService Auth, AuthAccountRecord Account) World(string databaseName, UserRecord user)
        => World(databaseName, user, out _);

    private static (AuthService Auth, AuthAccountRecord Account) World(
        string databaseName, UserRecord user, out Infrastructure.Persistence.AppDbContext db)
    {
        db = TestWorld.Db(databaseName);
        var account = TestWorld.Account(user, Password);
        db.Users.Add(user);
        db.AuthAccounts.Add(account);
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);

        return (new AuthService(TestWorld.Storage(), new SilentEmailSender(), services.BuildServiceProvider()), account);
    }

    /// <summary>
    /// 寄信在這幾組測試裡不是重點，但「有沒有寄」有時就是結論：
    /// 真的寄出去會讓測試依賴 SMTP，吞掉又會讓「被擋下」和「寄信失敗」長得一樣。
    /// 所以這裡成功回傳、什麼都不做，由測試自己去看資料庫有沒有產生 token。
    /// </summary>
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
    }
}
