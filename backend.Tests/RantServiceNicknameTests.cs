using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 牢騷牆上顯示的名字。
///
/// 為什麼需要這組測試：rant_posts.nickname 與 rant_replies.nickname 是發文當下
/// 抄下來的副本，而全專案沒有任何一處會回頭更新它們。線上的實際狀況是——同一個人
/// 的九篇貼文停在四個不同的舊名字，整面牆十七篇有十七篇的名字和作者現在的暱稱
/// 對不上。顯示改成即時從 users 取之後，這組測試釘的就是「舊貼文會不會跟著改名」。
///
/// 另一半是寫入：那兩欄留著當「發文當下這個人叫什麼」的稽核紀錄，所以不能再
/// 採信客戶端送上來的字串——以前 userId 取自 JWT、暱稱取自 request body，
/// 兩者從來沒有被對帳過。
/// </summary>
public sealed class RantServiceNicknameTests
{
    private const string Author = "user_author";
    private const string Replier = "user_replier";
    private const string PostId = "rant_under_test";

    private const string OldName = "456789";
    private const string NewName = "阿佑吞球者";

    // ── 顯示：舊資料要跟著改名 ──

    [Fact]
    public void OldPostShowsTheNameTheAuthorUsesNow()
    {
        var service = Build(nameof(OldPostShowsTheNameTheAuthorUsesNow), Named(Author, NewName));

        Assert.Equal(NewName, service.GetPublicPosts().Single(x => x.UserId == Author).Nickname);
    }

    [Fact]
    public void OldPostShowsTheNewNameOnItsOwnPageToo()
    {
        var service = Build(nameof(OldPostShowsTheNewNameOnItsOwnPageToo), Named(Author, NewName));

        Assert.Equal(NewName, service.GetPost(PostId)!.Nickname);
    }

    [Fact]
    public void OldReplyShowsTheNameTheReplierUsesNow()
    {
        var service = Build(
            nameof(OldReplyShowsTheNameTheReplierUsesNow),
            Named(Author, NewName),
            Named(Replier, "改過名的回覆者"));

        Assert.Equal("改過名的回覆者", service.GetPost(PostId)!.Replies.Single().Nickname);
    }

    /// <summary>
    /// 子回覆是巢狀的，換名要一路走到底。只看第一層的話，一串對話裡
    /// 上面是新名字、下面還是舊名字。
    /// </summary>
    [Fact]
    public void NestedRepliesAreRenamedAllTheWayDown()
    {
        const string name = nameof(NestedRepliesAreRenamedAllTheWayDown);
        var db = Seed(name, Named(Author, NewName), Named(Replier, "改過名的回覆者"));
        db.RantReplies.Add(new RantReplyRecord
        {
            Id = "reply_child",
            RantPostId = PostId,
            ParentReplyId = ReplyIdOf(Replier),
            UserId = Author,
            Nickname = OldName,
            Content = "謝謝你"
        });
        db.SaveChanges();

        var child = Service(db).GetPost(PostId)!.Replies.Single().Replies.Single();

        Assert.Equal(NewName, child.Nickname);
    }

    /// <summary>互動之後回傳的那一份，和牆上看到的要是同一份——名字也一樣。</summary>
    [Fact]
    public void ThePostHandedBackAfterAnInteractionIsRenamedToo()
    {
        var service = Build(
            nameof(ThePostHandedBackAfterAnInteractionIsRenamedToo),
            Named(Author, NewName),
            Named(Replier, "改過名的回覆者"));

        Assert.Equal(NewName, service.Understand(PostId, Replier)!.Nickname);
        Assert.Equal(NewName, service.LikeReply(PostId, ReplyIdOf(Replier), Replier)!.Nickname);
    }

    // ── 顯示：查不到名字的時候 ──

    /// <summary>
    /// 商用後台改資料那條路完全沒有驗證（AdminUserService 只判斷 != null，
    /// 輸入框可以清空），所以 users.nickname 真的可能是空字串。空字串掛在牆上
    /// 就是一個沒有名字的人在講話，退回發文當下那一份比較像話。
    /// </summary>
    [Fact]
    public void BlankCurrentNameFallsBackToTheNameAtPostingTime()
    {
        var service = Build(nameof(BlankCurrentNameFallsBackToTheNameAtPostingTime), Named(Author, "   "));

        Assert.Equal(OldName, service.GetPost(PostId)!.Nickname);
    }

    /// <summary>兩邊都空的時候才輪到預設值。</summary>
    [Fact]
    public void BlankOnBothSidesFallsBackToTheDefault()
    {
        const string name = nameof(BlankOnBothSidesFallsBackToTheDefault);
        var db = Seed(name, Named(Author, ""));
        db.RantPosts.Single(x => x.Id == PostId).Nickname = "";
        db.SaveChanges();

        Assert.Equal("同頻使用者", Service(db).GetPost(PostId)!.Nickname);
    }

    // ── 寫入：名字由伺服器決定 ──

    /// <summary>
    /// 發文時存下來的是 users 當下的暱稱，不是呼叫端給的任何東西。
    /// 這一欄現在的用途是稽核，記的是不是當事人自己報的名字就是重點。
    /// </summary>
    [Fact]
    public void PostingStoresTheNameFromTheUsersTable()
    {
        const string name = nameof(PostingStoresTheNameFromTheUsersTable);
        var db = Seed(name, Named(Author, NewName));

        var created = Service(db).Create(Author, "今天很累", RantMode.JUST_SAYING).Post;

        using var reloaded = TestWorld.Db(name);
        Assert.Equal(NewName, reloaded.RantPosts.Single(x => x.Id == created!.Id).Nickname);
    }

    [Fact]
    public void ReplyingStoresTheNameFromTheUsersTable()
    {
        const string name = nameof(ReplyingStoresTheNameFromTheUsersTable);
        var db = Seed(name, Named(Author, NewName), Named(Replier, "改過名的回覆者"));

        Service(db).Reply(PostId, Replier, "我懂");

        using var reloaded = TestWorld.Db(name);
        var written = reloaded.RantReplies.Single(x => x.Id != ReplyIdOf(Replier));
        Assert.Equal("改過名的回覆者", written.Nickname);
    }

    /// <summary>查無此人時不能寫出空名字——那一欄是 not null。</summary>
    [Fact]
    public void PostingByAnUnknownUserStoresTheDefault()
    {
        const string name = nameof(PostingByAnUnknownUserStoresTheDefault);
        var db = Seed(name, Named(Author, NewName));

        var created = Service(db).Create("user_not_in_the_table", "我是誰", RantMode.JUST_SAYING).Post;

        using var reloaded = TestWorld.Db(name);
        Assert.Equal("同頻使用者", reloaded.RantPosts.Single(x => x.Id == created!.Id).Nickname);
    }

    // ── 測試資料 ──

    private static string ReplyIdOf(string userId) => $"reply_{userId}";

    /// <summary>狀態正常、但暱稱已經改成別的名字的人。</summary>
    private static UserRecord Named(string id, string nickname)
    {
        var user = TestWorld.User(id);
        user.Nickname = nickname;
        return user;
    }

    /// <summary>
    /// 一篇貼文、一則回覆，兩者的 nickname 都停在舊名字——也就是線上資料的樣子。
    /// </summary>
    private static AppDbContext Seed(string databaseName, UserRecord author, UserRecord? replier = null)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.Add(author);
        db.RantPosts.Add(new RantPostRecord
        {
            Id = PostId,
            UserId = author.Id,
            Nickname = OldName,
            Content = "今天真的很累"
        });
        if (replier != null)
        {
            db.Users.Add(replier);
            db.RantReplies.Add(new RantReplyRecord
            {
                Id = ReplyIdOf(replier.Id),
                RantPostId = PostId,
                UserId = replier.Id,
                Nickname = "回覆者的舊名字",
                Content = "我懂"
            });
        }
        db.SaveChanges();
        return db;
    }

    private static RantService Service(AppDbContext db)
    {
        // RantService 自己從容器拿 AppDbContext；NotificationService 沒註冊，
        // 服務本來就允許它為 null。
        var services = new ServiceCollection();
        services.AddSingleton(db);

        return new RantService(
            TestWorld.Storage(),
            new TodayAnalyzerService(),
            new ModerationService(),
            services.BuildServiceProvider());
    }

    private static RantService Build(string databaseName, UserRecord author, UserRecord? replier = null)
        => Service(Seed(databaseName, author, replier));
}
