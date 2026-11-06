using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 被隱藏的貼文，以及檢舉／按讚記在誰名下。
///
/// 為什麼合成一組：這兩件事表面上無關，實際上串起來是一個洞。
/// PostIsVisible 原本只問作者的帳號狀態、不問 is_hidden，而 Report 裡的
/// `post.IsHidden = post.ReportCount >= 3` 是指派不是條件——一篇被後台處置
/// 隱藏的貼文（is_hidden=true、report_count=0）只要被任何一個登入者檢舉一次，
/// 就會被重新算成 1 >= 3 = false 而重見天日。
///
/// 而檢舉之所以隨便誰都打得進去，是因為 RantsController 從來沒把呼叫者的 userId
/// 傳下來，服務端就退回成 post.UserId。線上資料正是這個樣子：三列檢舉紀錄，
/// 三列的 user_id 都等於被檢舉貼文的作者。
/// </summary>
public sealed class RantServiceHiddenPostTests
{
    private const string Author = "user_author";
    private const string Reporter = "user_reporter";
    private const string PostId = "rant_under_test";
    private const string ReplyId = "reply_under_test";

    // ── 隱藏的貼文碰不得 ──

    /// <summary>看得見的時候四個端點都通，否則下面那條用「永遠回 null」也會綠。</summary>
    [Fact]
    public void AVisiblePostStillTakesEveryInteraction()
    {
        var service = Build(nameof(AVisiblePostStillTakesEveryInteraction), hidden: false);

        Assert.NotNull(service.Understand(PostId, Reporter));
        Assert.NotNull(service.Reply(PostId, Reporter, "還看得到嗎"));
        Assert.NotNull(service.LikeReply(PostId, ReplyId, Reporter));
        Assert.NotNull(service.Report(PostId, Reporter));
    }

    /// <summary>
    /// 隱藏之後四個端點全部不通。這四個都回傳整篇貼文（含內容、作者 id、所有回覆），
    /// 所以漏掉任何一個就等於替隱藏起來的內容開了一扇讀取後門。
    /// </summary>
    [Fact]
    public void AHiddenPostTakesNoInteractionAtAll()
    {
        const string name = nameof(AHiddenPostTakesNoInteractionAtAll);
        var service = Build(name, hidden: true);

        Assert.Null(service.Understand(PostId, Reporter));
        Assert.Null(service.Reply(PostId, Reporter, "還看得到嗎"));
        Assert.Null(service.LikeReply(PostId, ReplyId, Reporter));
        Assert.Null(service.Report(PostId, Reporter));

        // 回 null 不等於沒寫。這篇有兩道守衛——PostIsVisible 擋在最前面、BuildPost
        // 擋在回傳前——而只靠後者的話，回覆早就 Add + SaveChanges 進去了，愛心也加了，
        // 外面看起來一樣是 null。所以另開一個 context 回去數，釘的是「擋在寫入之前」。
        using var reloaded = TestWorld.Db(name);
        Assert.Single(reloaded.RantReplies.Where(x => x.RantPostId == PostId));
        Assert.Equal(0, reloaded.RantReplies.Single(x => x.Id == ReplyId).LikeCount);
        Assert.Empty(reloaded.RantReactions.Where(x => x.RantPostId == PostId));
    }

    /// <summary>
    /// 這一條是整組的重點：檢舉一篇已經被隱藏的貼文，不能把它解除隱藏。
    /// 回 null 不夠——要回去重讀，確認 is_hidden 還是 true、report_count 沒被動。
    /// </summary>
    [Fact]
    public void ReportingAHiddenPostDoesNotBringItBack()
    {
        const string name = nameof(ReportingAHiddenPostDoesNotBringItBack);
        var service = Build(name, hidden: true);

        Assert.Null(service.Report(PostId, Reporter));

        using var reloaded = TestWorld.Db(name);
        var post = reloaded.RantPosts.Single(x => x.Id == PostId);
        Assert.True(post.IsHidden);
        Assert.Equal(0, post.ReportCount);
        Assert.Empty(reloaded.RantReports.Where(x => x.RantPostId == PostId));
    }

    // ── 檢舉記在檢舉的人名下 ──

    [Fact]
    public void AReportIsFiledUnderTheReporterNotTheAuthor()
    {
        const string name = nameof(AReportIsFiledUnderTheReporterNotTheAuthor);
        var service = Build(name, hidden: false);

        service.Report(PostId, Reporter);

        using var reloaded = TestWorld.Db(name);
        Assert.Equal(Reporter, reloaded.RantReports.Single(x => x.RantPostId == PostId).UserId);
    }

    /// <summary>同一個人檢舉兩次只算一次——這是原本就對的部分，別修壞了。</summary>
    [Fact]
    public void TheSamePersonReportingTwiceOnlyCountsOnce()
    {
        const string name = nameof(TheSamePersonReportingTwiceOnlyCountsOnce);
        var service = Build(name, hidden: false);

        service.Report(PostId, Reporter);
        service.Report(PostId, Reporter);

        using var reloaded = TestWorld.Db(name);
        Assert.Equal(1, reloaded.RantPosts.Single(x => x.Id == PostId).ReportCount);
    }

    /// <summary>
    /// 三個不同的人檢舉就自動隱藏。這段在修好歸屬之前是死碼：第一筆檢舉寫在
    /// 作者自己名下，之後 Any() 恆為 true，report_count 永遠卡在 1。
    ///
    /// 第三次回 null 是對的——那一刻貼文已經隱藏，BuildPost 就撈不到了。
    /// </summary>
    [Fact]
    public void ThreeDifferentPeopleHideThePost()
    {
        const string name = nameof(ThreeDifferentPeopleHideThePost);
        var service = Build(name, hidden: false, extraUsers: ["user_r2", "user_r3"]);

        Assert.NotNull(service.Report(PostId, Reporter));
        Assert.NotNull(service.Report(PostId, "user_r2"));
        Assert.Null(service.Report(PostId, "user_r3"));

        using var reloaded = TestWorld.Db(name);
        var post = reloaded.RantPosts.Single(x => x.Id == PostId);
        Assert.Equal(3, post.ReportCount);
        Assert.True(post.IsHidden);
    }

    // ── 按讚記在按讚的人名下 ──

    /// <summary>
    /// 兩個人各按一次就是 2。退回成作者的話，第一個人按完就把那一列佔住，
    /// 之後誰按都是 1——整個 App 的樹洞愛心數封頂在 1。
    /// </summary>
    [Fact]
    public void TwoPeopleUnderstandingMakesTwo()
    {
        var service = Build(nameof(TwoPeopleUnderstandingMakesTwo), hidden: false, extraUsers: ["user_u2"]);

        service.Understand(PostId, Reporter);

        Assert.Equal(2, service.Understand(PostId, "user_u2")!.LikeCount);
    }

    [Fact]
    public void TheSamePersonUnderstandingTwiceStillCountsOnce()
    {
        var service = Build(nameof(TheSamePersonUnderstandingTwiceStillCountsOnce), hidden: false);

        service.Understand(PostId, Reporter);

        Assert.Equal(1, service.Understand(PostId, Reporter)!.LikeCount);
    }

    // ── 回覆的讚也要一人一次 ──

    /// <summary>
    /// 貼文的愛心一直都有去重，回覆的沒有——原本只是 reply.LikeCount += 1，
    /// 連按的人是誰都沒傳進來。同一個帳號可以一直按，上限只有速率限制。
    /// </summary>
    [Fact]
    public void TheSamePersonLikingAReplyTwiceOnlyCountsOnce()
    {
        const string name = nameof(TheSamePersonLikingAReplyTwiceOnlyCountsOnce);
        var service = Build(name, hidden: false);

        service.LikeReply(PostId, ReplyId, Reporter);
        var post = service.LikeReply(PostId, ReplyId, Reporter);

        Assert.Equal(1, post!.Replies.Single(x => x.Id == ReplyId).LikeCount);

        using var reloaded = TestWorld.Db(name);
        Assert.Single(reloaded.RantReactions.Where(x => x.RantReplyId == ReplyId));
    }

    [Fact]
    public void TwoPeopleLikingAReplyMakesTwo()
    {
        var service = Build(nameof(TwoPeopleLikingAReplyMakesTwo), hidden: false, extraUsers: ["user_l2"]);

        service.LikeReply(PostId, ReplyId, Reporter);
        var post = service.LikeReply(PostId, ReplyId, "user_l2");

        Assert.Equal(2, post!.Replies.Single(x => x.Id == ReplyId).LikeCount);
    }

    /// <summary>
    /// 回覆的讚與貼文的讚共用 rant_reactions，差別在 rant_reply_id 有沒有值。
    /// 同一個人在同一篇底下按貼文、又按底下的回覆，是兩列不同的紀錄——
    /// 舊的唯一索引（rant_post_id, user_id, reaction_type）會把第二列擋掉，
    /// 所以 migration 把它改成只管 rant_reply_id is null 的那一半。
    /// </summary>
    [Fact]
    public void LikingThePostAndLikingAReplyAreSeparateRecords()
    {
        const string name = nameof(LikingThePostAndLikingAReplyAreSeparateRecords);
        var service = Build(name, hidden: false);

        service.Understand(PostId, Reporter);
        service.LikeReply(PostId, ReplyId, Reporter);

        using var reloaded = TestWorld.Db(name);
        var rows = reloaded.RantReactions.Where(x => x.RantPostId == PostId).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Single(rows.Where(x => x.RantReplyId == null));
        Assert.Single(rows.Where(x => x.RantReplyId == ReplyId));
    }

    // ── 測試資料 ──

    /// <summary>一篇貼文、一則回覆，作者與檢舉者都是正常帳號，只有 is_hidden 由呼叫端指定。</summary>
    private static RantService Build(string databaseName, bool hidden, string[]? extraUsers = null)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.Add(TestWorld.User(Author));
        db.Users.Add(TestWorld.User(Reporter));
        foreach (var id in extraUsers ?? []) db.Users.Add(TestWorld.User(id));
        db.RantPosts.Add(new RantPostRecord
        {
            Id = PostId,
            UserId = Author,
            Nickname = Author,
            Content = "今天真的很累",
            IsHidden = hidden
        });
        db.RantReplies.Add(new RantReplyRecord
        {
            Id = ReplyId,
            RantPostId = PostId,
            UserId = Reporter,
            Nickname = Reporter,
            Content = "我懂"
        });
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);

        return new RantService(
            TestWorld.Storage(),
            new TodayAnalyzerService(),
            new ModerationService(),
            services.BuildServiceProvider());
    }
}
