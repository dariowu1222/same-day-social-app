using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 檢舉處理完要寄信給檢舉人與被檢舉人。
///
/// 為什麼兩邊都寄：檢舉的人需要知道自己的檢舉有沒有被看過，不然下次就不檢舉了；
/// 被檢舉的人需要知道自己的東西為什麼不見了——「哪天發現東西不見卻不知道
/// 發生過什麼事」是最會讓人離開的那一種體驗。
///
/// 這組測試釘的主要是**不該寄的時候不要寄**：那幾條比「有寄出去」難維護得多。
/// </summary>
public sealed class ModerationEmailTests
{
    private const string PostId = "rant_under_review";
    private const string Author = "user_author";
    private const string Reporter = "user_reporter";
    private const string Admin = "user_admin";
    private const string ReportId = "report_under_review";

    private const string AuthorEmail = "author@example.com";
    private const string ReporterEmail = "reporter@example.com";

    [Fact]
    public async Task UpholdingAReportMailsBothSides()
    {
        var (service, _, mail) = Build(nameof(UpholdingAReportMailsBothSides), ReportCategory.HARASSMENT);

        await service.ReviewAsync(ReportId, Admin, ReportStatus.RESOLVED, null, hideTarget: true);

        Assert.Equal([(ReporterEmail, true)], mail.ToReporter);
        Assert.Equal([(AuthorEmail, true, true)], mail.ToReported);
    }

    [Fact]
    public async Task DismissingAReportAlsoMailsBothSides()
    {
        var (service, _, mail) = Build(nameof(DismissingAReportAlsoMailsBothSides), ReportCategory.HARASSMENT);

        await service.ReviewAsync(ReportId, Admin, ReportStatus.DISMISSED, null, hideTarget: null);

        Assert.Equal([(ReporterEmail, false)], mail.ToReporter);
        Assert.Equal([(AuthorEmail, false, false)], mail.ToReported);
    }

    /// <summary>
    /// 自我傷害那種單子一封都不寄。那是替人求救，不是舉發違規——寄一封
    /// 「你被檢舉了」給一個可能正在受傷的人，傷害遠大於資訊價值。
    /// 替朋友送出的人也不該收到「已對該帳號做出處置」，那不是他按下去的意思。
    /// </summary>
    [Fact]
    public async Task ASelfHarmFlagMailsNobody()
    {
        var (service, _, mail) = Build(nameof(ASelfHarmFlagMailsNobody), ReportCategory.SELF_HARM);

        await service.ReviewAsync(ReportId, Admin, ReportStatus.RESOLVED, null, hideTarget: null);

        Assert.Empty(mail.ToReporter);
        Assert.Empty(mail.ToReported);
    }

    /// <summary>
    /// 後台的結案按鈕在送出期間沒有 disabled，連點兩下就是兩個請求，
    /// 「已處理」清單裡也還給得出「處理這筆」。狀態沒變就不該再寄一次。
    /// </summary>
    [Fact]
    public async Task ReviewingTwiceWithTheSameOutcomeOnlyMailsOnce()
    {
        var (service, _, mail) = Build(nameof(ReviewingTwiceWithTheSameOutcomeOnlyMailsOnce), ReportCategory.HARASSMENT);

        await service.ReviewAsync(ReportId, Admin, ReportStatus.RESOLVED, null, hideTarget: true);
        await service.ReviewAsync(ReportId, Admin, ReportStatus.RESOLVED, null, hideTarget: true);

        Assert.Single(mail.ToReporter);
        Assert.Single(mail.ToReported);
    }

    /// <summary>但改判了就要再寄一次——結果不一樣，當事人該知道。</summary>
    [Fact]
    public async Task ChangingTheOutcomeMailsAgain()
    {
        var (service, _, mail) = Build(nameof(ChangingTheOutcomeMailsAgain), ReportCategory.HARASSMENT);

        await service.ReviewAsync(ReportId, Admin, ReportStatus.RESOLVED, null, hideTarget: true);
        await service.ReviewAsync(ReportId, Admin, ReportStatus.DISMISSED, null, hideTarget: false);

        Assert.Equal([true, false], mail.ToReporter.Select(x => x.Upheld));
    }

    /// <summary>
    /// 寄信失敗不能讓審核失敗。信寄不出去頂多是當事人沒收到通知，
    /// 而處置本身已經生效了——為了一封信把處置整個回滾才是真的糟。
    /// </summary>
    [Fact]
    public async Task AFailingMailServerDoesNotBreakTheReview()
    {
        const string name = nameof(AFailingMailServerDoesNotBreakTheReview);
        var (service, db, _) = Build(name, ReportCategory.HARASSMENT, mail: new ExplodingEmailSender());

        var result = await service.ReviewAsync(ReportId, Admin, ReportStatus.RESOLVED, null, hideTarget: true);

        Assert.NotNull(result);
        Assert.True(db.RantPosts.Single().IsHidden);
        Assert.Equal(ReportStatus.RESOLVED.ToString(), db.Reports.Single().Status);
    }

    /// <summary>查不到信箱就跳過那個人，不要炸掉，也不要連另一邊都不寄。</summary>
    [Fact]
    public async Task SomebodyWithoutAnAccountIsSkippedNotCrashed()
    {
        const string name = nameof(SomebodyWithoutAnAccountIsSkippedNotCrashed);
        var (service, db, mail) = Build(name, ReportCategory.HARASSMENT);
        db.AuthAccounts.Remove(db.AuthAccounts.Single(x => x.UserId == Reporter));
        db.SaveChanges();

        await service.ReviewAsync(ReportId, Admin, ReportStatus.RESOLVED, null, hideTarget: true);

        Assert.Empty(mail.ToReporter);
        Assert.Single(mail.ToReported);
    }

    // ── 測試資料 ──

    private static (ReportService Service, AppDbContext Db, RecordingEmailSender Mail) Build(
        string databaseName, ReportCategory category, IEmailSender? mail = null)
    {
        var db = TestWorld.Db(databaseName);
        var author = TestWorld.User(Author);
        var reporter = TestWorld.User(Reporter);
        db.Users.AddRange(author, reporter);
        db.AuthAccounts.AddRange(
            new AuthAccountRecord { Id = "auth_author", UserId = Author, Username = AuthorEmail, PasswordHash = "x" },
            new AuthAccountRecord { Id = "auth_reporter", UserId = Reporter, Username = ReporterEmail, PasswordHash = "x" });
        db.RantPosts.Add(new RantPostRecord
        {
            Id = PostId, UserId = Author, Nickname = "阿佑", Content = "被檢舉的內容",
        });
        db.Reports.Add(new ReportRecord
        {
            Id = ReportId,
            TargetType = ReportTargetType.RANT_POST.ToString(),
            TargetId = PostId,
            ReportedUserId = Author,
            ReporterId = Reporter,
            Category = category.ToString(),
            Severity = 60,
            Status = ReportStatus.PENDING.ToString(),
        });
        db.SaveChanges();

        var recording = mail as RecordingEmailSender ?? new RecordingEmailSender();
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(mail ?? recording);
        return (new ReportService(services.BuildServiceProvider()), db, recording);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string Email, bool Upheld)> ToReporter { get; } = [];
        public List<(string Email, bool Upheld, bool Hidden)> ToReported { get; } = [];

        public Task SendReportOutcomeToReporterAsync(string toEmail, bool upheld, CancellationToken ct)
        {
            ToReporter.Add((toEmail, upheld));
            return Task.CompletedTask;
        }

        public Task SendReportOutcomeToReportedAsync(string toEmail, bool upheld, bool contentHidden, CancellationToken ct)
        {
            ToReported.Add((toEmail, upheld, contentHidden));
            return Task.CompletedTask;
        }

        public Task SendRegistrationVerificationCodeAsync(string toEmail, string code, CancellationToken ct) => Task.CompletedTask;
        public Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken ct) => Task.CompletedTask;
        public Task SendAdminLoginCodeAsync(string toEmail, string code, CancellationToken ct) => Task.CompletedTask;
        public Task SendAdminReverifyCodeAsync(string toEmail, string code, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ExplodingEmailSender : IEmailSender
    {
        public Task SendReportOutcomeToReporterAsync(string toEmail, bool upheld, CancellationToken ct)
            => throw new InvalidOperationException("SMTP 掛了");

        public Task SendReportOutcomeToReportedAsync(string toEmail, bool upheld, bool contentHidden, CancellationToken ct)
            => throw new InvalidOperationException("SMTP 掛了");

        public Task SendRegistrationVerificationCodeAsync(string toEmail, string code, CancellationToken ct) => Task.CompletedTask;
        public Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken ct) => Task.CompletedTask;
        public Task SendAdminLoginCodeAsync(string toEmail, string code, CancellationToken ct) => Task.CompletedTask;
        public Task SendAdminReverifyCodeAsync(string toEmail, string code, CancellationToken ct) => Task.CompletedTask;
    }
}
