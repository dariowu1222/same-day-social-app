using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 牢騷牆的帳號狀態過濾。
///
/// 為什麼這面牆特別要測：它是全 App 唯一不用登入就讀得到的內容。漏擋的話，
/// 一個已經按下「刪除帳號」的人，整串心事都還掛在公開頁面上。
///
/// 名字不在這組測試的範圍內——顯示用的暱稱已經改成即時從 users 取
/// （RantService.WithCurrentNames），註銷會把它洗成「已離開的使用者」。
/// 這裡擋的是內容本身。名字那條線在 RantServiceNicknameTests。
/// </summary>
public sealed class RantServiceAccountStatusTests
{
    private const string Author = "user_author";
    private const string Replier = "user_replier";

    /// <summary>
    /// 先確認這組測試真的看得到東西。少了這一個，其他「不應出現」的斷言
    /// 就算是因為整面牆壞掉而空的，也會一樣綠。
    /// </summary>
    [Fact]
    public void ActiveAuthorIsOnTheWall()
    {
        var service = Build(nameof(ActiveAuthorIsOnTheWall), TestWorld.User(Author));

        Assert.Contains(service.GetPublicPosts(), x => x.UserId == Author);
    }

    [Fact]
    public void DeletedAuthorIsNotOnTheWall()
    {
        var service = Build(nameof(DeletedAuthorIsNotOnTheWall), TestWorld.User(Author, AccountStatus.DELETED));

        Assert.DoesNotContain(service.GetPublicPosts(), x => x.UserId == Author);
    }

    [Fact]
    public void BannedAuthorIsNotOnTheWall()
    {
        var service = Build(nameof(BannedAuthorIsNotOnTheWall), TestWorld.User(Author, AccountStatus.BANNED));

        Assert.DoesNotContain(service.GetPublicPosts(), x => x.UserId == Author);
    }

    /// <summary>停權還沒到期：擋住。這是下一個測試的對照組。</summary>
    [Fact]
    public void RunningSuspensionIsNotOnTheWall()
    {
        var service = Build(
            nameof(RunningSuspensionIsNotOnTheWall),
            TestWorld.User(Author, AccountStatus.SUSPENDED, TestWorld.StillSuspended));

        Assert.DoesNotContain(service.GetPublicPosts(), x => x.UserId == Author);
    }

    /// <summary>停權到期：貼文回到牆上，不必等後台有人去按。</summary>
    [Fact]
    public void ExpiredSuspensionReturnsToTheWall()
    {
        var service = Build(
            nameof(ExpiredSuspensionReturnsToTheWall),
            TestWorld.User(Author, AccountStatus.SUSPENDED, TestWorld.SuspensionOver));

        Assert.Contains(service.GetPublicPosts(), x => x.UserId == Author);
    }

    /// <summary>直接帶貼文網址進來也要擋，否則擋掉的只是「不好找」而已。</summary>
    [Fact]
    public void DeletedAuthorsPostIsNotReachableByLink()
    {
        var service = Build(
            nameof(DeletedAuthorsPostIsNotReachableByLink),
            TestWorld.User(Author, AccountStatus.DELETED));

        Assert.Null(service.GetPost(PostId));
    }

    // ── 回覆 ──

    [Fact]
    public void ActiveReplierIsShown()
    {
        var service = Build(
            nameof(ActiveReplierIsShown),
            TestWorld.User(Author),
            TestWorld.User(Replier));

        Assert.Contains(service.GetPost(PostId)!.Replies, x => x.UserId == Replier);
    }

    [Fact]
    public void DeletedReplierIsHiddenButThePostStays()
    {
        var service = Build(
            nameof(DeletedReplierIsHiddenButThePostStays),
            TestWorld.User(Author),
            TestWorld.User(Replier, AccountStatus.DELETED));

        var post = service.GetPost(PostId);

        Assert.NotNull(post);
        Assert.DoesNotContain(post!.Replies, x => x.UserId == Replier);
    }

    [Fact]
    public void ExpiredSuspensionBringsTheReplyBack()
    {
        var service = Build(
            nameof(ExpiredSuspensionBringsTheReplyBack),
            TestWorld.User(Author),
            TestWorld.User(Replier, AccountStatus.SUSPENDED, TestWorld.SuspensionOver));

        Assert.Contains(service.GetPost(PostId)!.Replies, x => x.UserId == Replier);
    }

    // ── 看不到就不能動 ──

    /// <summary>
    /// 牆上濾掉了，互動也要跟著不通。不然拿著舊畫面上的 id，照樣能往一篇
    /// 已經隱形的貼文裡寫東西、按讚、檢舉。
    ///
    /// 四個互動端點全部在這裡列出來，不是為了湊齊。這組斷言原本只有三條
    /// （Understand／Reply／Report），而少掉的那一條正好就是當時唯一沒掛
    /// PostIsVisible 的那個端點——測試漏的和程式漏的是同一個。
    /// </summary>
    [Fact]
    public void CannotInteractWithAHiddenPost()
    {
        var service = Build(
            nameof(CannotInteractWithAHiddenPost),
            TestWorld.User(Author, AccountStatus.BANNED),
            TestWorld.User(Replier));

        Assert.Null(service.Understand(PostId, Replier));
        Assert.Null(service.Reply(PostId, Replier, "還看得到嗎"));
        Assert.Null(service.Report(PostId, Replier));
        Assert.Null(service.LikeReply(PostId, ReplyIdOf(Replier), Replier));
    }

    [Fact]
    public void CanStillInteractWithAVisiblePost()
    {
        var service = Build(
            nameof(CanStillInteractWithAVisiblePost),
            TestWorld.User(Author),
            TestWorld.User(Replier));

        Assert.NotNull(service.Understand(PostId, Replier));
        Assert.NotNull(service.Reply(PostId, Replier, "還看得到嗎"));
        Assert.NotNull(service.Report(PostId, Replier));
        Assert.NotNull(service.LikeReply(PostId, ReplyIdOf(Replier), Replier));
    }

    /// <summary>
    /// 按讚回覆這條路特別再測一次，因為它洩漏的東西和其他三個不同。
    ///
    /// Understand／Reply／Report 漏擋，頂多是讓人往隱形的貼文裡寫東西；
    /// LikeReply 漏擋是把整篇貼文連同所有回覆原封不動回傳給呼叫者——內容、
    /// 作者 id、暱稱都在裡面。這一條釘的是「什麼都不回來」，不是「少按一個讚」。
    /// </summary>
    [Fact]
    public void LikingAReplyDoesNotHandBackABannedAuthorsPost()
    {
        var service = Build(
            nameof(LikingAReplyDoesNotHandBackABannedAuthorsPost),
            TestWorld.User(Author, AccountStatus.BANNED),
            TestWorld.User(Replier));

        Assert.Null(service.LikeReply(PostId, ReplyIdOf(Replier), Replier));
    }

    /// <summary>
    /// 回 null 不等於沒寫。
    ///
    /// 這篇貼文的守衛有兩道：LikeReply 開頭的 PostIsVisible，以及 BuildPost 裡
    /// 對作者狀態的檢查。只靠後者的話，讚其實已經加上去、SaveChanges 也跑完了，
    /// 只是結果沒交出來——外面看起來一樣是 null，資料庫裡卻多了一個讚。
    /// 所以這一條另開一個 context 回去重讀，釘的是「擋在寫入之前」。
    /// </summary>
    [Fact]
    public void LikingAReplyOnAHiddenPostDoesNotEvenCountTheLike()
    {
        const string name = nameof(LikingAReplyOnAHiddenPostDoesNotEvenCountTheLike);
        var service = Build(name, TestWorld.User(Author, AccountStatus.BANNED), TestWorld.User(Replier));

        Assert.Null(service.LikeReply(PostId, ReplyIdOf(Replier), Replier));

        using var reloaded = TestWorld.Db(name);
        Assert.Equal(0, reloaded.RantReplies.Single(x => x.Id == ReplyIdOf(Replier)).LikeCount);
    }

    /// <summary>停權到期之後按讚要恢復正常，否則上面那條用「永遠回 null」也會綠。</summary>
    [Fact]
    public void LikingAReplyWorksOnceTheSuspensionIsOver()
    {
        var service = Build(
            nameof(LikingAReplyWorksOnceTheSuspensionIsOver),
            TestWorld.User(Author, AccountStatus.SUSPENDED, TestWorld.SuspensionOver),
            TestWorld.User(Replier));

        var post = service.LikeReply(PostId, ReplyIdOf(Replier), Replier);

        Assert.NotNull(post);
        Assert.Equal(1, post!.Replies.Single(x => x.UserId == Replier).LikeCount);
    }

    // ── 測試資料 ──

    private const string PostId = "rant_under_test";

    private static string ReplyIdOf(string userId) => $"reply_{userId}";

    /// <summary>
    /// 一篇貼文、一則回覆，作者與回覆者的狀態由呼叫端指定。
    /// 只傳作者時不放回覆。
    /// </summary>
    private static RantService Build(string databaseName, UserRecord author, UserRecord? replier = null)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.Add(author);
        db.RantPosts.Add(new RantPostRecord
        {
            Id = PostId,
            UserId = author.Id,
            Nickname = author.Nickname,
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
                Nickname = replier.Nickname,
                Content = "我懂"
            });
        }
        db.SaveChanges();

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
}
