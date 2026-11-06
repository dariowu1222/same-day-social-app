using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 後台把貼文隱藏之後，要退得回去。
///
/// 真實發生過的事（線上審核軌跡 2026-11-06 13:43）：管理者按「隱藏內容並結案」，
/// 發現判斷錯了，回到「已處理」清單想救回來，按了「駁回檢舉」——貼文仍然是隱藏的。
/// 原因是後台那三顆按鈕送的 hideTarget 只有 true 與 null，而 null 的語意是
/// 「這次不要動隱藏狀態」，所以 SetTargetHidden 根本沒被呼叫。
/// 後端一直做得到，只是沒有人叫得動它。
///
/// 另一半是退回之後站不站得住：自動隱藏的門檻是「三個不同的人檢舉過」，
/// 而那個計數不會因為後台判定不成立就減少。
/// </summary>
public sealed class ModerationRestoreTests
{
    private const string PostId = "rant_under_review";
    private const string Author = "user_author";
    private const string Admin = "user_admin";
    private const string ReportId = "report_under_review";

    [Fact]
    public void HidingThroughAReviewStillWorks()
    {
        var (service, db) = Build(nameof(HidingThroughAReviewStillWorks), hidden: false, reportCount: 1);

        service.Review(ReportId, Admin, ReportStatus.RESOLVED, null, hideTarget: true);

        Assert.True(db.RantPosts.Single().IsHidden);
    }

    [Fact]
    public void ReviewingWithFalseGivesThePostBack()
    {
        var (service, db) = Build(nameof(ReviewingWithFalseGivesThePostBack), hidden: true, reportCount: 3);

        service.Review(ReportId, Admin, ReportStatus.DISMISSED, null, hideTarget: false);

        Assert.False(db.RantPosts.Single().IsHidden);
    }

    /// <summary>
    /// 退回之後要站得住。不歸零的話，貼文回去之後只要再多一個人檢舉，
    /// ReportCount 就從 3 變 4、立刻又被自動隱藏——對原作者來說是「它又不見了」，
    /// 而且沒有人動過後台。
    /// </summary>
    [Fact]
    public void GivingThePostBackAlsoClearsTheReportCount()
    {
        var (service, db) = Build(nameof(GivingThePostBackAlsoClearsTheReportCount), hidden: true, reportCount: 3);

        service.Review(ReportId, Admin, ReportStatus.DISMISSED, null, hideTarget: false);

        Assert.Equal(0, db.RantPosts.Single().ReportCount);
    }

    /// <summary>
    /// 退回之後真的再被檢舉一次，不該立刻又消失。
    /// 這一條走的是真正的 RantService.Report，不是只看欄位。
    /// </summary>
    [Fact]
    public void AfterBeingGivenBackOneMoreReportDoesNotHideItAgain()
    {
        const string name = nameof(AfterBeingGivenBackOneMoreReportDoesNotHideItAgain);
        var (service, db) = Build(name, hidden: true, reportCount: 3);
        service.Review(ReportId, Admin, ReportStatus.DISMISSED, null, hideTarget: false);

        var rants = Rants(db);
        db.Users.Add(TestWorld.User("user_fourth_reporter"));
        db.SaveChanges();
        rants.Report(PostId, "user_fourth_reporter");

        var post = db.RantPosts.Single();
        Assert.False(post.IsHidden);
        Assert.Equal(1, post.ReportCount);
    }

    /// <summary>「不動內容」那顆（hideTarget 為 null）要真的不動——結案不等於隱藏。</summary>
    [Fact]
    public void ReviewingWithNullLeavesTheContentAlone()
    {
        var (service, db) = Build(nameof(ReviewingWithNullLeavesTheContentAlone), hidden: true, reportCount: 3);

        service.Review(ReportId, Admin, ReportStatus.RESOLVED, null, hideTarget: null);

        Assert.True(db.RantPosts.Single().IsHidden);
        Assert.Equal(3, db.RantPosts.Single().ReportCount);
    }

    // ── 退回之後要真的站得住（走 App 真正在用的那條路）──

    /// <summary>
    /// 這是真正會咬人的那一條。ApplyAutoModeration 不看 rant_posts.report_count，
    /// 它每次有新檢舉進來都把這篇「非 DISMISSED」的檢舉權重重新加總一次跟門檻比
    /// ——而「結案」是 RESOLVED，仍然計入。所以只歸零 report_count 沒有用：
    /// 舊的那幾筆還在累計，下一筆檢舉一進來就立刻又超過門檻。
    /// </summary>
    [Fact]
    public void AfterBeingGivenBackANewReportThroughTheRealPathDoesNotHideItAgain()
    {
        const string name = nameof(AfterBeingGivenBackANewReportThroughTheRealPathDoesNotHideItAgain);
        var (service, db) = Build(name, hidden: true, reportCount: 0);
        // 三筆騷擾（各 60 分，合計 180）已經超過 150 的門檻，而且都是「結案」不是「駁回」
        AddResolvedHarassment(db, "report_a", "user_r1");
        AddResolvedHarassment(db, "report_b", "user_r2");
        AddResolvedHarassment(db, "report_c", "user_r3");

        service.Review(ReportId, Admin, ReportStatus.DISMISSED, null, hideTarget: false);
        service.Submit("user_r4", ReportTargetType.RANT_POST, PostId, ReportCategory.HARASSMENT, null);

        Assert.False(db.RantPosts.Single().IsHidden);
    }

    /// <summary>沒有退回過的貼文，權重照常累積——別把自動隱藏修壞了。</summary>
    [Fact]
    public void WithoutARestoreTheWeightsStillAddUpAndHideThePost()
    {
        const string name = nameof(WithoutARestoreTheWeightsStillAddUpAndHideThePost);
        var (service, db) = Build(name, hidden: false, reportCount: 0);
        AddResolvedHarassment(db, "report_a", "user_r1");
        AddResolvedHarassment(db, "report_b", "user_r2");

        service.Submit("user_r3", ReportTargetType.RANT_POST, PostId, ReportCategory.HARASSMENT, null);

        Assert.True(db.RantPosts.Single().IsHidden);
    }

    /// <summary>管理者自己這一筆的狀態不能被順手蓋掉。</summary>
    [Fact]
    public void RestoringDoesNotOverwriteTheStatusTheAdminChose()
    {
        const string name = nameof(RestoringDoesNotOverwriteTheStatusTheAdminChose);
        var (service, db) = Build(name, hidden: true, reportCount: 0);

        service.Review(ReportId, Admin, ReportStatus.RESOLVED, null, hideTarget: false);

        Assert.Equal(ReportStatus.RESOLVED.ToString(), db.Reports.Single(x => x.Id == ReportId).Status);
    }

    /// <summary>
    /// 自我傷害那種單子不跟著被判定為不成立。它本來就不計入自動隱藏的權重，
    /// 而且那是一張關懷單，不是內容違規的指控。
    /// </summary>
    [Fact]
    public void RestoringLeavesASelfHarmFlagAlone()
    {
        const string name = nameof(RestoringLeavesASelfHarmFlagAlone);
        var (service, db) = Build(name, hidden: true, reportCount: 0);
        db.Reports.Add(new ReportRecord
        {
            Id = "report_welfare",
            TargetType = ReportTargetType.RANT_POST.ToString(),
            TargetId = PostId,
            ReportedUserId = Author,
            ReporterId = "user_worried",
            Category = ReportCategory.SELF_HARM.ToString(),
            Severity = 100,
            Status = ReportStatus.PENDING.ToString(),
        });
        db.SaveChanges();

        service.Review(ReportId, Admin, ReportStatus.DISMISSED, null, hideTarget: false);

        Assert.Equal(
            ReportStatus.PENDING.ToString(),
            db.Reports.Single(x => x.Id == "report_welfare").Status);
    }

    // ── 後台要看得到目前狀態 ──

    /// <summary>
    /// 佇列原本不顯示「這篇現在是不是隱藏中」，審核的人無從判斷該按隱藏還是退回。
    /// 整批一次查完，不要每一列各問一次。
    /// </summary>
    [Fact]
    public void TheQueueCanSeeWhichTargetsAreHidden()
    {
        const string name = nameof(TheQueueCanSeeWhichTargetsAreHidden);
        var (service, db) = Build(name, hidden: true, reportCount: 3);
        db.RantPosts.Add(new RantPostRecord { Id = "rant_visible", UserId = Author, Nickname = "阿佑", Content = "還在" });
        db.SaveChanges();

        var hidden = service.HiddenRantPosts([PostId, "rant_visible", "rant_does_not_exist"]);

        Assert.True(hidden[PostId]);
        Assert.False(hidden["rant_visible"]);
        Assert.False(hidden.ContainsKey("rant_does_not_exist"));
    }

    [Fact]
    public void AskingAboutNothingCostsNothing()
    {
        var (service, _) = Build(nameof(AskingAboutNothingCostsNothing), hidden: true, reportCount: 1);

        Assert.Empty(service.HiddenRantPosts([]));
        Assert.Empty(service.HiddenRantPosts(["", "   "]));
    }

    // ── 測試資料 ──

    /// <summary>一筆已結案的騷擾檢舉。權重 60，三筆就超過 150 的自動隱藏門檻。</summary>
    private static void AddResolvedHarassment(AppDbContext db, string id, string reporterId)
    {
        db.Reports.Add(new ReportRecord
        {
            Id = id,
            TargetType = ReportTargetType.RANT_POST.ToString(),
            TargetId = PostId,
            ReportedUserId = Author,
            ReporterId = reporterId,
            Category = ReportCategory.HARASSMENT.ToString(),
            Severity = 60,
            Status = ReportStatus.RESOLVED.ToString(),
        });
        db.SaveChanges();
    }

    private static (ReportService Service, AppDbContext Db) Build(string databaseName, bool hidden, int reportCount)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.Add(TestWorld.User(Author));
        db.RantPosts.Add(new RantPostRecord
        {
            Id = PostId,
            UserId = Author,
            Nickname = "阿佑",
            Content = "被檢舉的內容",
            IsHidden = hidden,
            ReportCount = reportCount,
        });
        db.Reports.Add(new ReportRecord
        {
            Id = ReportId,
            TargetType = ReportTargetType.RANT_POST.ToString(),
            TargetId = PostId,
            ReportedUserId = Author,
            ReporterId = "user_reporter",
            Category = ReportCategory.OTHER.ToString(),
            Severity = 20,
            Status = ReportStatus.PENDING.ToString(),
        });
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        return (new ReportService(services.BuildServiceProvider()), db);
    }

    private static RantService Rants(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        return new RantService(
            TestWorld.Storage(), new TodayAnalyzerService(), new ModerationService(), services.BuildServiceProvider());
    }
}
