using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 樹洞的刪除、收回、隱藏。
///
/// 三條規則一起改的（2026-11-25）：
///  1. 刪主貼文照 Threads：貼文不見，底下別人的回應留著
///  2. 貼文作者可以隱藏自己貼文底下的回應
///  3. 刪除與收回都不是當場刪列，內容多留 30 天給檢舉與後台查證
///
/// 這幾件事的共通點是「同一篇貼文，不同的人看到的不一樣」，所以這裡的斷言
/// 幾乎都是成對的：某個人看得到、另一個人看不到。
/// </summary>
public sealed class RantSoftDeleteTests
{
    private const string Author = "user_author";
    private const string Replier = "user_replier";
    private const string Bystander = "user_bystander";
    private const string PostId = "rant_under_test";

    // ── 收回回應 ──

    /// <summary>
    /// 以前收回是當場刪列：留一句難聽的話、等對方看到就收回，之後誰也查不到。
    /// </summary>
    [Fact]
    public void ATakenBackReplyIsGoneForEveryoneButStillOnRecord()
    {
        var db = Seed(nameof(ATakenBackReplyIsGoneForEveryoneButStillOnRecord));
        AddReply(db, "nasty", Replier);
        db.SaveChanges();
        var service = Service(db);

        service.DeleteReply(PostId, "nasty", Replier);

        Assert.Empty(service.GetPost(PostId, Author)!.Replies);
        Assert.Empty(service.GetPost(PostId, Replier)!.Replies);
        Assert.Empty(Assert.Single(service.GetPublicPosts()).Replies);
        var row = db.RantReplies.Single(x => x.Id == "nasty");
        Assert.NotNull(row.DeletedAt);
        Assert.Equal("nasty", row.Content);
    }

    /// <summary>收回之後還能被檢舉：內容快照拿得到，後台才有東西可以看。</summary>
    [Fact]
    public void ATakenBackReplyCanStillBeReported()
    {
        var db = Seed(nameof(ATakenBackReplyCanStillBeReported));
        AddReply(db, "nasty", Replier);
        db.SaveChanges();
        Service(db).DeleteReply(PostId, "nasty", Replier);

        var result = Reports(db).Submit(Author, ReportTargetType.RANT_REPLY, "nasty", ReportCategory.HARASSMENT, null);

        Assert.Null(result.Error);
        Assert.Equal("nasty", result.Report!.ContextSnapshot);
        Assert.Equal(Replier, result.Report.ReportedUserId);
    }

    [Fact]
    public void YouCannotAnswerOrHeartAReplyThatWasTakenBack()
    {
        var db = Seed(nameof(YouCannotAnswerOrHeartAReplyThatWasTakenBack));
        AddReply(db, "gone", Replier);
        db.SaveChanges();
        var service = Service(db);
        service.DeleteReply(PostId, "gone", Replier);

        Assert.Null(service.Reply(PostId, Bystander, "接話", parentReplyId: "gone"));
        Assert.Null(service.LikeReply(PostId, "gone", Bystander));
        Assert.Equal(RantService.ReplyNotFound, service.DeleteReply(PostId, "gone", Replier).Error);
        Assert.Equal(1, db.RantReplies.Count());
    }

    /// <summary>底下那一則自己也收回了，上面這一則就又收得回來。</summary>
    [Fact]
    public void AReplyWhoseOnlyAnswerWasTakenBackCanBeTakenBackToo()
    {
        var db = Seed(nameof(AReplyWhoseOnlyAnswerWasTakenBackCanBeTakenBackToo));
        AddReply(db, "mine", Replier);
        AddReply(db, "answer", Bystander, parentReplyId: "mine");
        db.SaveChanges();
        var service = Service(db);
        service.DeleteReply(PostId, "answer", Bystander);

        var (_, error) = service.DeleteReply(PostId, "mine", Replier);

        Assert.Null(error);
    }

    // ── 刪主貼文 ──

    [Fact]
    public void ADeletedPostLeavesTheWallButItsRepliesStay()
    {
        var db = Seed(nameof(ADeletedPostLeavesTheWallButItsRepliesStay));
        AddReply(db, "kind", Replier);
        db.SaveChanges();
        var service = Service(db);

        Assert.True(service.Delete(PostId, Author));

        Assert.Empty(service.GetPublicPosts());
        var shell = service.GetPost(PostId, Bystander)!;
        Assert.True(shell.IsDeleted);
        Assert.Equal(["kind"], shell.Replies.Select(x => x.Id));
        Assert.Equal(1, db.RantReplies.Count());
    }

    /// <summary>殼裡不能剩下任何貼文作者的東西——包括他是誰。</summary>
    [Fact]
    public void TheShellOfADeletedPostCarriesNothingOfTheAuthor()
    {
        var db = Seed(nameof(TheShellOfADeletedPostCarriesNothingOfTheAuthor));
        var record = db.RantPosts.Single();
        record.HashTags = ["工作"];
        record.ImageUrls = ["https://storage.test/a.jpg"];
        record.ImageUrl = "https://storage.test/a.jpg";
        record.AudioUrl = "https://storage.test/a.m4a";
        AddReply(db, "kind", Replier);
        db.SaveChanges();
        var service = Service(db);
        service.Understand(PostId, Bystander);
        service.Delete(PostId, Author);

        // 連作者自己帶著連結回來，拿到的也是同一個殼
        var shell = service.GetPost(PostId, Author)!;

        Assert.Equal("", shell.Content);
        Assert.Equal("", shell.Nickname);
        Assert.Equal("", shell.UserId);
        Assert.Empty(shell.HashTags);
        Assert.Empty(shell.ImageUrls);
        Assert.Null(shell.ImageUrl);
        Assert.Null(shell.AudioUrl);
        Assert.Equal(0, shell.LikeCount);
    }

    /// <summary>底下沒有回應的貼文刪掉之後，連殼都不必有。</summary>
    [Fact]
    public void ADeletedPostWithNoRepliesIsSimplyGone()
    {
        var db = Seed(nameof(ADeletedPostWithNoRepliesIsSimplyGone));
        var service = Service(db);

        service.Delete(PostId, Author);

        Assert.Null(service.GetPost(PostId, Author));
        Assert.Null(service.GetPost(PostId));
    }

    /// <summary>內容還留在資料庫裡——那 30 天是給後台查證用的。</summary>
    [Fact]
    public void ADeletedPostKeepsItsContentOnRecord()
    {
        var db = Seed(nameof(ADeletedPostKeepsItsContentOnRecord));
        var service = Service(db);

        service.Delete(PostId, Author);

        var row = db.RantPosts.Single();
        Assert.NotNull(row.DeletedAt);
        Assert.Equal("今天真的很累", row.Content);
    }

    [Fact]
    public void OnlyTheAuthorCanDeleteAndOnlyOnce()
    {
        var db = Seed(nameof(OnlyTheAuthorCanDeleteAndOnlyOnce));
        var service = Service(db);

        Assert.False(service.Delete(PostId, Bystander));
        Assert.Null(db.RantPosts.Single().DeletedAt);
        Assert.True(service.Delete(PostId, Author));
        Assert.False(service.Delete(PostId, Author));
    }

    /// <summary>刪掉之後不能再往這篇加東西；底下既有的回應還能收回、還能按愛心。</summary>
    [Fact]
    public void ADeletedPostTakesNoNewRepliesButOldOnesStillWork()
    {
        var db = Seed(nameof(ADeletedPostTakesNoNewRepliesButOldOnesStillWork));
        AddReply(db, "first", Replier);
        AddReply(db, "second", Bystander);
        db.SaveChanges();
        var service = Service(db);
        service.Delete(PostId, Author);

        Assert.Null(service.Reply(PostId, Bystander, "還想說"));
        Assert.Null(service.Understand(PostId, Bystander));
        Assert.Equal(RantService.ReplyNotFound, service.SetReplyHidden(PostId, "first", Author, true).Error);

        var liked = service.LikeReply(PostId, "first", Bystander)!;
        Assert.Equal(1, liked.Replies.Single(x => x.Id == "first").LikeCount);

        var (afterTakeBack, error) = service.DeleteReply(PostId, "first", Replier);
        Assert.Null(error);
        Assert.Equal(["second"], afterTakeBack!.Replies.Select(x => x.Id));
    }

    /// <summary>收回的是最後一則：成功，而且這一串已經沒有東西可看——不是錯誤。</summary>
    [Fact]
    public void TakingBackTheLastReplyUnderADeletedPostSucceedsWithNothingLeft()
    {
        var db = Seed(nameof(TakingBackTheLastReplyUnderADeletedPostSucceedsWithNothingLeft));
        AddReply(db, "only", Replier);
        db.SaveChanges();
        var service = Service(db);
        service.Delete(PostId, Author);

        var (post, error) = service.DeleteReply(PostId, "only", Replier);

        Assert.Null(error);
        Assert.Null(post);
        Assert.Null(service.GetPost(PostId, Replier));
    }

    /// <summary>被後台處置隱藏的貼文，作者刪了也不會變成一個看得到回應的殼。</summary>
    [Fact]
    public void AModeratedPostDoesNotComeBackAsAShell()
    {
        var db = Seed(nameof(AModeratedPostDoesNotComeBackAsAShell));
        AddReply(db, "kind", Replier);
        db.RantPosts.Single().IsHidden = true;
        db.SaveChanges();
        var service = Service(db);

        service.Delete(PostId, Author);

        Assert.Null(service.GetPost(PostId, Replier));
        Assert.Null(service.LikeReply(PostId, "kind", Bystander));
    }

    // ── 貼文作者隱藏回應 ──

    [Fact]
    public void AReplyTheAuthorHidIsSeenOnlyByTheAuthorAndItsWriter()
    {
        var db = Seed(nameof(AReplyTheAuthorHidIsSeenOnlyByTheAuthorAndItsWriter));
        AddReply(db, "nasty", Replier);
        AddReply(db, "kind", Bystander);
        db.SaveChanges();
        var service = Service(db);

        var (forAuthor, error) = service.SetReplyHidden(PostId, "nasty", Author, true);

        Assert.Null(error);
        var hidden = forAuthor!.Replies.Single(x => x.Id == "nasty");
        Assert.True(hidden.IsHidden);
        Assert.True(hidden.CanUnhide);
        Assert.False(forAuthor.Replies.Single(x => x.Id == "kind").IsHidden);

        // 寫的人自己還看得到，而且看不出被藏了
        var forWriter = service.GetPost(PostId, Replier)!.Replies.Single(x => x.Id == "nasty");
        Assert.False(forWriter.IsHidden);
        Assert.False(forWriter.CanUnhide);

        Assert.Equal(["kind"], service.GetPost(PostId, Bystander)!.Replies.Select(x => x.Id));
        Assert.Equal(["kind"], service.GetPost(PostId)!.Replies.Select(x => x.Id));
        Assert.Equal(["kind"], Assert.Single(service.GetPublicPosts()).Replies.Select(x => x.Id));
        Assert.Equal(1, Assert.Single(service.GetPublicPosts()).ReplyCount);
    }

    /// <summary>
    /// 接在底下的那幾則引用的就是被藏那一則的原文，只藏上面等於原文還掛在別人的回應裡。
    /// </summary>
    [Fact]
    public void HidingAReplyHidesWhatWasSaidUnderIt()
    {
        var db = Seed(nameof(HidingAReplyHidesWhatWasSaidUnderIt));
        AddReply(db, "nasty", Replier);
        AddReply(db, "answer", Bystander, parentReplyId: "nasty");
        db.SaveChanges();
        var service = Service(db);

        var (forAuthor, _) = service.SetReplyHidden(PostId, "nasty", Author, true);

        Assert.Empty(service.GetPost(PostId)!.Replies);
        var answer = forAuthor!.Replies.Single().Replies.Single();
        Assert.True(answer.IsHidden);
        // 不是親手藏的這一則，要取消得取消上面那一則
        Assert.False(answer.CanUnhide);

        // 接話的人自己還看得到自己那一則，但看不到被藏的那一則
        var forAnswerer = service.GetPost(PostId, Bystander)!;
        Assert.Equal(["answer"], forAnswerer.Replies.Select(x => x.Id));
        Assert.Empty(forAnswerer.Replies.Single().Replies);
    }

    [Fact]
    public void TheAuthorCanUnhide()
    {
        var db = Seed(nameof(TheAuthorCanUnhide));
        AddReply(db, "reply", Replier);
        db.SaveChanges();
        var service = Service(db);
        service.SetReplyHidden(PostId, "reply", Author, true);

        var (post, error) = service.SetReplyHidden(PostId, "reply", Author, false);

        Assert.Null(error);
        Assert.False(Assert.Single(post!.Replies).IsHidden);
        Assert.Single(service.GetPost(PostId)!.Replies);
        var row = db.RantReplies.Single();
        Assert.Null(row.HiddenAt);
        Assert.Null(row.HiddenBy);
    }

    /// <summary>這是貼文作者的權限，不是任何人的；也不能拿來藏自己的回應（那是收回）。</summary>
    [Fact]
    public void OnlyThePostAuthorCanHideAndNotTheirOwnReply()
    {
        var db = Seed(nameof(OnlyThePostAuthorCanHideAndNotTheirOwnReply));
        AddReply(db, "theirs", Replier);
        AddReply(db, "authors", Author);
        db.SaveChanges();
        var service = Service(db);

        Assert.Equal(RantService.ReplyNotFound, service.SetReplyHidden(PostId, "theirs", Bystander, true).Error);
        Assert.Equal(RantService.ReplyNotFound, service.SetReplyHidden(PostId, "theirs", Replier, true).Error);
        Assert.Equal(RantService.ReplyNotFound, service.SetReplyHidden(PostId, "authors", Author, true).Error);
        Assert.Equal(RantService.ReplyNotFound, service.SetReplyHidden(PostId, "no_such_reply", Author, true).Error);
        Assert.All(db.RantReplies.ToList(), x => Assert.Null(x.HiddenAt));
    }

    // ── 後台隱藏回應 ──

    /// <summary>後台藏的誰都看不到——貼文作者、寫的人都一樣——而且貼文作者不能取消。</summary>
    [Fact]
    public async Task AReplyTheAdminHidIsSeenByNobody()
    {
        var db = Seed(nameof(AReplyTheAdminHidIsSeenByNobody));
        AddReply(db, "nasty", Replier);
        db.SaveChanges();
        var service = Service(db);
        var reports = Reports(db);
        var report = reports.Submit(Author, ReportTargetType.RANT_REPLY, "nasty", ReportCategory.HARASSMENT, null).Report!;

        await reports.ReviewAsync(report.Id, "admin", ReportStatus.RESOLVED, null, hideTarget: true);

        Assert.Empty(service.GetPost(PostId, Author)!.Replies);
        Assert.Empty(service.GetPost(PostId, Replier)!.Replies);
        Assert.Empty(service.GetPost(PostId)!.Replies);
        Assert.True(reports.HiddenRantReplies(["nasty"])["nasty"]);
        Assert.Equal(RantService.ReplyNotFound, service.SetReplyHidden(PostId, "nasty", Author, false).Error);
        Assert.NotNull(db.RantReplies.Single().HiddenAt);
    }

    /// <summary>作者先自己藏了再檢舉，後台按「隱藏內容」要把它升級成誰都看不到。</summary>
    [Fact]
    public async Task TheAdminCanTakeOverAReplyTheAuthorAlreadyHid()
    {
        var db = Seed(nameof(TheAdminCanTakeOverAReplyTheAuthorAlreadyHid));
        AddReply(db, "nasty", Replier);
        db.SaveChanges();
        var service = Service(db);
        var reports = Reports(db);
        service.SetReplyHidden(PostId, "nasty", Author, true);
        var report = reports.Submit(Author, ReportTargetType.RANT_REPLY, "nasty", ReportCategory.HARASSMENT, null).Report!;

        // 作者自己藏的，在後台看來還不算「已隱藏」——寫的人還看得到
        Assert.False(reports.HiddenRantReplies(["nasty"])["nasty"]);
        Assert.Contains("貼文作者", reports.RantTargetNotes([], ["nasty"])["nasty"]);

        await reports.ReviewAsync(report.Id, "admin", ReportStatus.RESOLVED, null, hideTarget: true);

        Assert.Empty(service.GetPost(PostId, Replier)!.Replies);
        Assert.Empty(service.GetPost(PostId, Author)!.Replies);
    }

    [Fact]
    public async Task TheAdminCanGiveAHiddenReplyBack()
    {
        var db = Seed(nameof(TheAdminCanGiveAHiddenReplyBack));
        AddReply(db, "fine", Replier);
        db.SaveChanges();
        var service = Service(db);
        var reports = Reports(db);
        var report = reports.Submit(Bystander, ReportTargetType.RANT_REPLY, "fine", ReportCategory.OTHER, null).Report!;
        await reports.ReviewAsync(report.Id, "admin", ReportStatus.RESOLVED, null, hideTarget: true);

        await reports.ReviewAsync(report.Id, "admin", ReportStatus.DISMISSED, null, hideTarget: false);

        Assert.Single(service.GetPost(PostId)!.Replies);
        Assert.False(reports.HiddenRantReplies(["fine"])["fine"]);
    }

    /// <summary>後台要看得出「這則已經被本人拿掉了」，而且知道內容還會留到哪一天。</summary>
    [Fact]
    public void TheAdminIsToldWhenTheTargetWasRemovedByItsOwner()
    {
        var db = Seed(nameof(TheAdminIsToldWhenTheTargetWasRemovedByItsOwner));
        AddReply(db, "nasty", Replier);
        AddReply(db, "untouched", Bystander);
        db.SaveChanges();
        var service = Service(db);
        service.DeleteReply(PostId, "nasty", Replier);
        service.Delete(PostId, Author);

        var notes = Reports(db).RantTargetNotes([PostId], ["nasty", "untouched"]);

        Assert.Contains("刪除", notes[PostId]);
        Assert.Contains("收回", notes["nasty"]);
        Assert.False(notes.ContainsKey("untouched"));
    }

    // ── 牆上只帶摘要 ──

    /// <summary>
    /// 新版 App 的列表只要則數和前三個名字，回應等點開才抓。
    /// 則數要跟點開之後看到的一樣多——包括「這個人看不到的不算」。
    /// </summary>
    [Fact]
    public void TheWallSummaryCountsWhatThisViewerWouldSeeAndSendsNoReplies()
    {
        var db = Seed(nameof(TheWallSummaryCountsWhatThisViewerWouldSeeAndSendsNoReplies));
        AddReply(db, "kind", Replier);
        AddReply(db, "nasty", Bystander);
        AddReply(db, "answer", Replier, parentReplyId: "nasty");
        AddReply(db, "gone", Replier);
        db.SaveChanges();
        var service = Service(db);
        service.SetReplyHidden(PostId, "nasty", Author, true);
        service.DeleteReply(PostId, "gone", Replier);

        var forStranger = Assert.Single(service.GetPublicPosts(repliesAsSummary: true));
        var forAuthor = Assert.Single(service.GetPublicPosts(viewerId: Author, repliesAsSummary: true));

        Assert.Empty(forStranger.Replies);
        Assert.Equal(1, forStranger.ReplyCount);
        Assert.Equal([Replier], forStranger.ReplyNames);
        Assert.Equal(service.GetPost(PostId)!.ReplyCount, forStranger.ReplyCount);

        // 作者看得到自己藏起來的那兩則，所以他的則數比較多
        Assert.Empty(forAuthor.Replies);
        Assert.Equal(3, forAuthor.ReplyCount);
        Assert.Equal(service.GetPost(PostId, Author)!.ReplyCount, forAuthor.ReplyCount);
        Assert.Equal([Replier, Bystander], forAuthor.ReplyNames);
    }

    /// <summary>已經上架的 App 不帶那個參數，拿到的要跟以前一樣：回應整包、沒有 ReplyNames。</summary>
    [Fact]
    public void WithoutAskingForASummaryTheWallStillSendsEveryReply()
    {
        var db = Seed(nameof(WithoutAskingForASummaryTheWallStillSendsEveryReply));
        AddReply(db, "kind", Replier);
        db.SaveChanges();

        var post = Assert.Single(Service(db).GetPublicPosts());

        Assert.Equal("kind", Assert.Single(post.Replies).Content);
        Assert.Null(post.ReplyNames);
    }

    /// <summary>疊頭像只要三個，而且同一個人回很多則只算一次。</summary>
    [Fact]
    public void TheWallSummaryNamesAtMostThreeDistinctPeople()
    {
        var db = Seed(nameof(TheWallSummaryNamesAtMostThreeDistinctPeople));
        db.Users.Add(TestWorld.User("user_fourth"));
        db.Users.Add(TestWorld.User("user_fifth"));
        AddReply(db, "r1", Replier);
        AddReply(db, "r2", Replier);
        AddReply(db, "r3", Bystander);
        AddReply(db, "r4", "user_fourth");
        AddReply(db, "r5", "user_fifth");
        db.SaveChanges();

        var post = Assert.Single(Service(db).GetPublicPosts(repliesAsSummary: true));

        Assert.Equal(5, post.ReplyCount);
        Assert.Equal([Replier, Bystander, "user_fourth"], post.ReplyNames);
    }

    // ── 30 天 ──

    [Fact]
    public void NothingIsPurgedBeforeThirtyDays()
    {
        var db = Seed(nameof(NothingIsPurgedBeforeThirtyDays));
        AddReply(db, "gone", Replier);
        db.SaveChanges();
        db.RantReplies.Single().DeletedAt = DateTimeOffset.UtcNow.AddDays(-29);
        db.RantPosts.Single().DeletedAt = DateTimeOffset.UtcNow.AddDays(-29);
        db.SaveChanges();

        var outcome = Service(db).PurgeDeleted();

        Assert.Equal(new RantPurgeOutcome(0, 0, 0, true), outcome);
        Assert.Equal(1, db.RantReplies.Count());
        Assert.Equal("今天真的很累", db.RantPosts.Single().Content);
    }

    [Fact]
    public void ATakenBackReplyIsReallyGoneAfterThirtyDays()
    {
        var db = Seed(nameof(ATakenBackReplyIsReallyGoneAfterThirtyDays));
        AddReply(db, "old", Replier);
        AddReply(db, "recent", Replier);
        AddReply(db, "alive", Bystander);
        db.SaveChanges();
        db.RantReplies.Single(x => x.Id == "old").DeletedAt = DateTimeOffset.UtcNow.AddDays(-31);
        db.RantReplies.Single(x => x.Id == "recent").DeletedAt = DateTimeOffset.UtcNow.AddDays(-3);
        db.SaveChanges();

        var outcome = Service(db).PurgeDeleted();

        Assert.Equal(1, outcome.Replies);
        Assert.Equal(["alive", "recent"], db.RantReplies.Select(x => x.Id).OrderBy(x => x).ToList());
    }

    [Fact]
    public void ADeletedPostWithNoRepliesIsReallyGoneAfterThirtyDays()
    {
        var db = Seed(nameof(ADeletedPostWithNoRepliesIsReallyGoneAfterThirtyDays));
        var service = Service(db);
        service.Understand(PostId, Bystander);
        db.RantPosts.Single().DeletedAt = DateTimeOffset.UtcNow.AddDays(-31);
        db.SaveChanges();

        var outcome = service.PurgeDeleted();

        Assert.Equal(1, outcome.Posts);
        Assert.Empty(db.RantPosts.ToList());
        Assert.Empty(db.RantReactions.ToList());
    }

    /// <summary>
    /// 底下還有別人的回應：貼文的內容清掉，列留著當殼。整列刪掉的話，外鍵會把
    /// 那幾則回應一起帶走——那是別人的話，不是貼文作者的。
    /// </summary>
    [Fact]
    public void ADeletedPostWithRepliesIsEmptiedNotRemovedAfterThirtyDays()
    {
        var db = Seed(nameof(ADeletedPostWithRepliesIsEmptiedNotRemovedAfterThirtyDays));
        var record = db.RantPosts.Single();
        record.HashTags = ["工作"];
        record.ImageUrls = ["https://storage.test/a.jpg"];
        AddReply(db, "kind", Replier);
        db.SaveChanges();
        var service = Service(db);
        record.DeletedAt = DateTimeOffset.UtcNow.AddDays(-31);
        db.SaveChanges();

        var outcome = service.PurgeDeleted();

        Assert.Equal(new RantPurgeOutcome(0, 0, 1, true), outcome);
        var row = db.RantPosts.Single();
        Assert.Equal("", row.Content);
        Assert.Equal("", row.Nickname);
        Assert.Empty(row.HashTags);
        Assert.Empty(row.ImageUrls);
        Assert.Equal(["kind"], service.GetPost(PostId)!.Replies.Select(x => x.Id));

        // 再跑一次不會再算一次；等底下的回應都沒了，殼才會被刪掉
        Assert.Equal(new RantPurgeOutcome(0, 0, 0, true), service.PurgeDeleted());
        db.RantReplies.Single().DeletedAt = DateTimeOffset.UtcNow.AddDays(-31);
        db.SaveChanges();
        Assert.Equal(new RantPurgeOutcome(1, 1, 0, true), service.PurgeDeleted());
        Assert.Empty(db.RantPosts.ToList());
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

    private static ReportService Reports(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        return new ReportService(services.BuildServiceProvider());
    }
}
