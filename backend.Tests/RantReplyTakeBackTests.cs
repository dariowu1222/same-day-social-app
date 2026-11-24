using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 收回回應，以及愛心「我按過了沒」。
///
/// 為什麼兩件事放一起測：收回一則回應要連它身上的愛心一起刪，而愛心那張表
/// （rant_reactions）同時記貼文與回覆兩種對象，靠 rant_reply_id 是否為 null 分開。
/// 這裡原本有一個漏洞——算貼文愛心數、判斷「按過這篇了沒」都沒有帶那個條件，
/// 所以回覆的愛心會被加進貼文的數字，按過任何一則回覆的人也再按不了貼文。
/// </summary>
public sealed class RantReplyTakeBackTests
{
    private const string Author = "user_author";
    private const string Replier = "user_replier";
    private const string Bystander = "user_bystander";
    private const string PostId = "rant_under_test";

    [Fact]
    public void YouCanTakeBackYourOwnReplyWhenNobodyAnsweredIt()
    {
        var db = Seed(nameof(YouCanTakeBackYourOwnReplyWhenNobodyAnsweredIt));
        AddReply(db, "mine", Replier);
        AddReply(db, "theirs", Bystander);
        db.SaveChanges();

        var (post, error) = Service(db).DeleteReply(PostId, "mine", Replier);

        Assert.Null(error);
        Assert.Equal(["theirs"], post!.Replies.Select(x => x.Id));
        Assert.Equal(1, post.ReplyCount);
        Assert.False(db.RantReplies.Any(x => x.Id == "mine"));
    }

    /// <summary>「不是你的」和「不存在」要回同一個錯，不能讓人拿來探測 replyId。</summary>
    [Fact]
    public void SomeoneElsesReplyLooksLikeItDoesNotExist()
    {
        var db = Seed(nameof(SomeoneElsesReplyLooksLikeItDoesNotExist));
        AddReply(db, "theirs", Bystander);
        db.SaveChanges();
        var service = Service(db);

        var notMine = service.DeleteReply(PostId, "theirs", Replier);
        var missing = service.DeleteReply(PostId, "no_such_reply", Replier);

        Assert.Equal(RantService.ReplyNotFound, notMine.Error);
        Assert.Equal(RantService.ReplyNotFound, missing.Error);
        Assert.True(db.RantReplies.Any(x => x.Id == "theirs"));
    }

    /// <summary>連貼文作者也不能收回別人的回應——這不是版主功能。</summary>
    [Fact]
    public void ThePostAuthorCannotTakeBackOtherPeoplesReplies()
    {
        var db = Seed(nameof(ThePostAuthorCannotTakeBackOtherPeoplesReplies));
        AddReply(db, "theirs", Replier);
        db.SaveChanges();

        var (_, error) = Service(db).DeleteReply(PostId, "theirs", Author);

        Assert.Equal(RantService.ReplyNotFound, error);
        Assert.True(db.RantReplies.Any(x => x.Id == "theirs"));
    }

    /// <summary>
    /// 底下已經有人接話就不給收：連帶刪等於替別人把話收回去，
    /// 只刪這一則則會讓底下的 parent_reply_id 懸空。
    /// </summary>
    [Fact]
    public void AReplySomeoneAlreadyAnsweredStays()
    {
        var db = Seed(nameof(AReplySomeoneAlreadyAnsweredStays));
        AddReply(db, "mine", Replier);
        AddReply(db, "answer", Bystander, parentReplyId: "mine");
        db.SaveChanges();

        var (post, error) = Service(db).DeleteReply(PostId, "mine", Replier);

        Assert.Null(post);
        Assert.Equal(RantService.ReplyHasChildren, error);
        Assert.Equal(2, db.RantReplies.Count());
    }

    [Fact]
    public void TakingAReplyBackTakesItsHeartsWithIt()
    {
        var db = Seed(nameof(TakingAReplyBackTakesItsHeartsWithIt));
        AddReply(db, "mine", Replier);
        AddReply(db, "theirs", Bystander);
        db.SaveChanges();
        var service = Service(db);
        service.LikeReply(PostId, "mine", Bystander);
        service.LikeReply(PostId, "theirs", Replier);

        service.DeleteReply(PostId, "mine", Replier);

        // 留下來的只有另一則回覆身上的那一顆
        var left = Assert.Single(db.RantReactions.ToList());
        Assert.Equal("theirs", left.RantReplyId);
    }

    [Fact]
    public void AHiddenPostGivesNothingBack()
    {
        var db = Seed(nameof(AHiddenPostGivesNothingBack));
        AddReply(db, "mine", Replier);
        db.RantPosts.Single().IsHidden = true;
        db.SaveChanges();

        var (post, error) = Service(db).DeleteReply(PostId, "mine", Replier);

        Assert.Null(post);
        Assert.Equal(RantService.ReplyNotFound, error);
        Assert.True(db.RantReplies.Any(x => x.Id == "mine"));
    }

    // ── 愛心 ──

    [Fact]
    public void AHeartOnAReplyIsNotAHeartOnThePost()
    {
        var db = Seed(nameof(AHeartOnAReplyIsNotAHeartOnThePost));
        AddReply(db, "reply", Replier);
        db.SaveChanges();
        var service = Service(db);

        var afterReplyHeart = service.LikeReply(PostId, "reply", Bystander)!;

        Assert.Equal(0, afterReplyHeart.LikeCount);
        Assert.Equal(1, Assert.Single(afterReplyHeart.Replies).LikeCount);
        Assert.Equal(0, service.GetPost(PostId)!.LikeCount);
        Assert.Equal(0, Assert.Single(service.GetPublicPosts()).LikeCount);
    }

    /// <summary>按過底下某一則回覆，不該讓你再也按不了這篇貼文。</summary>
    [Fact]
    public void LikingAReplyDoesNotUseUpYourHeartForThePost()
    {
        var db = Seed(nameof(LikingAReplyDoesNotUseUpYourHeartForThePost));
        AddReply(db, "reply", Replier);
        db.SaveChanges();
        var service = Service(db);
        service.LikeReply(PostId, "reply", Bystander);

        var post = service.Understand(PostId, Bystander)!;

        Assert.Equal(1, post.LikeCount);
        Assert.Equal(1, service.Understand(PostId, Bystander)!.LikeCount);
    }

    [Fact]
    public void MarkLikedTellsThePostFromItsReplies()
    {
        var db = Seed(nameof(MarkLikedTellsThePostFromItsReplies));
        AddReply(db, "liked", Replier);
        AddReply(db, "untouched", Replier);
        db.SaveChanges();
        var service = Service(db);
        service.LikeReply(PostId, "liked", Bystander);

        var posts = service.GetPublicPosts();
        service.MarkLiked(posts, Bystander);

        var post = Assert.Single(posts);
        Assert.False(post.IsLiked);
        Assert.True(post.Replies.Single(x => x.Id == "liked").IsLiked);
        Assert.False(post.Replies.Single(x => x.Id == "untouched").IsLiked);

        service.Understand(PostId, Bystander);
        var again = service.GetPublicPosts();
        service.MarkLiked(again, Bystander);
        Assert.True(Assert.Single(again).IsLiked);
    }

    /// <summary>別人按的不算我按的。</summary>
    [Fact]
    public void MarkLikedIsAboutTheCallerOnly()
    {
        var db = Seed(nameof(MarkLikedIsAboutTheCallerOnly));
        AddReply(db, "reply", Replier);
        db.SaveChanges();
        var service = Service(db);
        service.Understand(PostId, Bystander);
        service.LikeReply(PostId, "reply", Bystander);

        var posts = service.GetPublicPosts();
        service.MarkLiked(posts, Replier);

        var post = Assert.Single(posts);
        Assert.False(post.IsLiked);
        Assert.False(Assert.Single(post.Replies).IsLiked);
    }

    // ── 收回愛心 ──

    [Fact]
    public void YouCanTakeYourHeartBackFromAPost()
    {
        var db = Seed(nameof(YouCanTakeYourHeartBackFromAPost));
        var service = Service(db);
        service.Understand(PostId, Bystander);
        service.Understand(PostId, Replier);

        var post = service.Unlike(PostId, Bystander)!;

        Assert.Equal(1, post.LikeCount);
        var left = Assert.Single(db.RantReactions.ToList());
        Assert.Equal(Replier, left.UserId);
    }

    /// <summary>沒按過的人戳這支，不能把別人的愛心扣掉；連戳兩下也一樣。</summary>
    [Fact]
    public void TakingBackAHeartYouNeverGaveChangesNothing()
    {
        var db = Seed(nameof(TakingBackAHeartYouNeverGaveChangesNothing));
        AddReply(db, "reply", Replier);
        db.SaveChanges();
        var service = Service(db);
        service.Understand(PostId, Replier);
        service.LikeReply(PostId, "reply", Author);

        var post = service.Unlike(PostId, Bystander)!;
        post = service.UnlikeReply(PostId, "reply", Bystander)!;
        post = service.UnlikeReply(PostId, "reply", Bystander)!;

        Assert.Equal(1, post.LikeCount);
        Assert.Equal(1, Assert.Single(post.Replies).LikeCount);
        Assert.Equal(2, db.RantReactions.Count());
    }

    [Fact]
    public void YouCanTakeYourHeartBackFromAReply()
    {
        var db = Seed(nameof(YouCanTakeYourHeartBackFromAReply));
        AddReply(db, "reply", Replier);
        db.SaveChanges();
        var service = Service(db);
        service.LikeReply(PostId, "reply", Bystander);
        service.LikeReply(PostId, "reply", Author);

        var post = service.UnlikeReply(PostId, "reply", Bystander)!;

        Assert.Equal(1, Assert.Single(post.Replies).LikeCount);
        var left = Assert.Single(db.RantReactions.ToList());
        Assert.Equal(Author, left.UserId);
    }

    /// <summary>收回貼文的愛心，不該連同一個人按在底下回覆上的愛心一起拿掉；反過來也是。</summary>
    [Fact]
    public void ThePostHeartAndTheReplyHeartComeBackSeparately()
    {
        var db = Seed(nameof(ThePostHeartAndTheReplyHeartComeBackSeparately));
        AddReply(db, "reply", Replier);
        db.SaveChanges();
        var service = Service(db);
        service.Understand(PostId, Bystander);
        service.LikeReply(PostId, "reply", Bystander);

        var afterPost = service.Unlike(PostId, Bystander)!;
        Assert.Equal(0, afterPost.LikeCount);
        Assert.Equal(1, Assert.Single(afterPost.Replies).LikeCount);

        service.Understand(PostId, Bystander);
        var afterReply = service.UnlikeReply(PostId, "reply", Bystander)!;
        Assert.Equal(1, afterReply.LikeCount);
        Assert.Equal(0, Assert.Single(afterReply.Replies).LikeCount);
    }

    /// <summary>收回之後可以再按回去，數字回到原樣。</summary>
    [Fact]
    public void AHeartTakenBackCanBeGivenAgain()
    {
        var db = Seed(nameof(AHeartTakenBackCanBeGivenAgain));
        var service = Service(db);
        service.Understand(PostId, Bystander);
        service.Unlike(PostId, Bystander);

        var post = service.Understand(PostId, Bystander)!;

        Assert.Equal(1, post.LikeCount);
        var posts = service.GetPublicPosts();
        service.MarkLiked(posts, Bystander);
        Assert.True(Assert.Single(posts).IsLiked);
    }

    private static int tick;

    private static void AddReply(AppDbContext db, string id, string userId, string? parentReplyId = null)
    {
        db.RantReplies.Add(new RantReplyRecord
        {
            Id = id,
            RantPostId = PostId,
            ParentReplyId = parentReplyId,
            UserId = userId,
            Nickname = userId,
            Content = id,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-60 + Interlocked.Increment(ref tick))
        });
    }

    private static AppDbContext Seed(string databaseName)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.Add(TestWorld.User(Author));
        db.Users.Add(TestWorld.User(Replier));
        db.Users.Add(TestWorld.User(Bystander));
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
