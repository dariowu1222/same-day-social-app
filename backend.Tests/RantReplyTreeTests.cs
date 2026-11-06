using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 回覆樹的形狀。
///
/// 為什麼要測：rant_replies.parent_reply_id 寫入時沒有任何檢查——不驗存不存在、
/// 不驗是不是同一篇、不限深度。而回傳的 JSON 一則回覆佔兩層，System.Text.Json
/// 的預設 MaxDepth 是 64，所以大約第 31 層就會讓 GET /api/rants 序列化丟例外。
/// 那支沒有 [Authorize]，掛掉就是整面牆對所有人掛掉。線上目前最深七層。
///
/// 攤平成兩層不改變畫面：前端本來就把子孫 flattenReplies 成單層再渲染。
/// </summary>
public sealed class RantReplyTreeTests
{
    private const string Author = "user_author";
    private const string PostId = "rant_under_test";

    [Fact]
    public void ADeepChainComesBackAsTwoLevels()
    {
        // 第一層一則，底下串十層
        var db = Seed(nameof(ADeepChainComesBackAsTwoLevels));
        AddChain(db, depth: 11, author: Author);
        db.SaveChanges();

        var post = Service(db).GetPost(PostId)!;

        var top = Assert.Single(post.Replies);
        Assert.Equal(10, top.Replies.Count);
        Assert.All(top.Replies, r => Assert.Empty(r.Replies));
        Assert.Equal(11, post.ReplyCount);
    }

    /// <summary>
    /// 攤平的順序要跟前端原本自己攤的一樣（深度優先），不然同一串對話
    /// 讀起來會變成另一個順序。
    /// </summary>
    [Fact]
    public void FlatteningKeepsDepthFirstOrder()
    {
        //  a
        //  ├ b
        //  │ └ c
        //  └ d
        var db = Seed(nameof(FlatteningKeepsDepthFirstOrder));
        Add(db, "a", null, 1);
        Add(db, "b", "a", 2);
        Add(db, "c", "b", 3);
        Add(db, "d", "a", 4);
        db.SaveChanges();

        var top = Assert.Single(Service(db).GetPost(PostId)!.Replies);

        Assert.Equal("a", top.Id);
        Assert.Equal(["b", "c", "d"], top.Replies.Select(x => x.Id));
    }

    /// <summary>引用框靠 parentReplyId 畫，攤平之後它必須還在。</summary>
    [Fact]
    public void ParentReplyIdSurvivesTheFlattening()
    {
        var db = Seed(nameof(ParentReplyIdSurvivesTheFlattening));
        Add(db, "a", null, 1);
        Add(db, "b", "a", 2);
        Add(db, "c", "b", 3);
        db.SaveChanges();

        var subs = Service(db).GetPost(PostId)!.Replies.Single().Replies;

        Assert.Equal("a", subs.Single(x => x.Id == "b").ParentReplyId);
        Assert.Equal("b", subs.Single(x => x.Id == "c").ParentReplyId);
    }

    /// <summary>
    /// 父親被帳號狀態濾掉時，子回覆以前會整串從樹上掉下去——既沒被渲染、
    /// 又還算在 replyCount 裡，畫面上就是「顯示 2 則，點開只有 1 則」。
    /// 現在它自己升成第一層。它的作者沒被處置，沒有理由連坐。
    /// </summary>
    [Fact]
    public void AReplyWhoseParentWasFilteredOutIsPromotedNotDropped()
    {
        const string gone = "user_banned";
        const string stays = "user_ok";
        var db = Seed(nameof(AReplyWhoseParentWasFilteredOutIsPromotedNotDropped));
        db.Users.Add(TestWorld.User(gone, AccountStatus.BANNED));
        db.Users.Add(TestWorld.User(stays));
        Add(db, "a", null, 1, gone);
        Add(db, "b", "a", 2, stays);
        db.SaveChanges();

        var post = Service(db).GetPost(PostId)!;

        var only = Assert.Single(post.Replies);
        Assert.Equal("b", only.Id);
        Assert.Equal(1, post.ReplyCount);
    }

    /// <summary>
    /// 升上來的孤兒要排在它該在的時間位置，不是被掃到最後面。
    /// 一串對話照時間讀才讀得懂，中間那一則跳到結尾就不是同一段對話了。
    /// </summary>
    [Fact]
    public void APromotedOrphanKeepsItsPlaceInTime()
    {
        const string gone = "user_banned";
        var db = Seed(nameof(APromotedOrphanKeepsItsPlaceInTime));
        db.Users.Add(TestWorld.User(gone, AccountStatus.BANNED));
        Add(db, "a", null, 1);
        Add(db, "x", null, 2, gone);   // 這一則會被濾掉
        Add(db, "b", "x", 3);          // 它的子回覆，要升成第一層
        Add(db, "c", null, 4);
        db.SaveChanges();

        var post = Service(db).GetPost(PostId)!;

        Assert.Equal(["a", "b", "c"], post.Replies.Select(x => x.Id));
    }

    /// <summary>parent_reply_id 指到別篇貼文的回覆也一樣——那一則在這篇裡就是第一層。</summary>
    [Fact]
    public void AParentFromAnotherPostIsTreatedAsNoParent()
    {
        var db = Seed(nameof(AParentFromAnotherPostIsTreatedAsNoParent));
        Add(db, "a", "reply_on_some_other_post", 1);
        db.SaveChanges();

        var post = Service(db).GetPost(PostId)!;

        Assert.Equal("a", Assert.Single(post.Replies).Id);
        Assert.Equal(1, post.ReplyCount);
    }

    /// <summary>
    /// 成環的資料不能讓它轉不出來，也不能讓它憑空消失——replyCount 是
    /// 畫面上那個數字，少一則就對不起來。
    /// </summary>
    [Fact]
    public void ACycleIsPlacedInsteadOfLoopingOrVanishing()
    {
        var db = Seed(nameof(ACycleIsPlacedInsteadOfLoopingOrVanishing));
        Add(db, "a", "b", 1);
        Add(db, "b", "a", 2);
        db.SaveChanges();

        var post = Service(db).GetPost(PostId)!;

        Assert.Equal(2, post.ReplyCount);
        Assert.Equal(2, post.Replies.Count + post.Replies.Sum(x => x.Replies.Count));
    }

    // ── 測試資料 ──

    private static void Add(AppDbContext db, string id, string? parentId, int minutes, string? userId = null)
        => db.RantReplies.Add(new RantReplyRecord
        {
            Id = id,
            RantPostId = PostId,
            ParentReplyId = parentId,
            UserId = userId ?? Author,
            Nickname = userId ?? Author,
            Content = id,
            CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(minutes)
        });

    private static void AddChain(AppDbContext db, int depth, string author)
    {
        for (var i = 0; i < depth; i++)
        {
            Add(db, $"r{i}", i == 0 ? null : $"r{i - 1}", i + 1, author);
        }
    }

    private static AppDbContext Seed(string databaseName)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.Add(TestWorld.User(Author));
        db.RantPosts.Add(new RantPostRecord
        {
            Id = PostId,
            UserId = Author,
            Nickname = Author,
            Content = "今天真的很累"
        });
        db.SaveChanges();
        return db;
    }

    private static RantService Service(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);

        return new RantService(
            TestWorld.Storage(),
            new TodayAnalyzerService(),
            new ModerationService(),
            services.BuildServiceProvider());
    }
}
