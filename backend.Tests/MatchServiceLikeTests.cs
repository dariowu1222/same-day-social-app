using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 右滑時對方已經不在了。
///
/// 牌是先發後滑的：卡片發到手上之後對方才被封鎖／註銷／停權，右滑仍然走得到
/// Like。先前唯一的防線在 ChatService.CreateRoom，而它丟的例外被 CompleteMatch
/// 的 catch 吞成一行 log——配對那兩列已經標記成互相、通知也送出去了，使用者
/// 看到的是「配對成功」加上一間進不去的聊天室，重按幾次結果一模一樣。
///
/// 所以這組測試釘的不只是回傳值，還有「資料庫裡不該留下半成立的配對」。
/// </summary>
public sealed class MatchServiceLikeTests
{
    private const string Me = "user_me";
    private const string Other = "user_other";
    private const string MatchId = "match_me_other";

    [Fact]
    public void ActiveCandidateCanBeLiked()
    {
        var (service, _) = Build(nameof(ActiveCandidateCanBeLiked), AccountStatus.ACTIVE, null);

        var outcome = service.Like(MatchId, Me);

        Assert.Equal(LikeStatus.Ok, outcome.Status);
        Assert.NotNull(outcome.Result);
    }

    [Fact]
    public void DeletedCandidateIsReportedAsUnavailable()
    {
        var (service, _) = Build(nameof(DeletedCandidateIsReportedAsUnavailable), AccountStatus.DELETED, null);

        var outcome = service.Like(MatchId, Me);

        Assert.Equal(LikeStatus.CandidateUnavailable, outcome.Status);
        Assert.Null(outcome.Result);
    }

    [Fact]
    public void BannedCandidateIsReportedAsUnavailable()
    {
        var (service, _) = Build(nameof(BannedCandidateIsReportedAsUnavailable), AccountStatus.BANNED, null);

        Assert.Equal(LikeStatus.CandidateUnavailable, service.Like(MatchId, Me).Status);
    }

    [Fact]
    public void RunningSuspensionIsReportedAsUnavailable()
    {
        var (service, _) = Build(
            nameof(RunningSuspensionIsReportedAsUnavailable), AccountStatus.SUSPENDED, TestWorld.StillSuspended);

        Assert.Equal(LikeStatus.CandidateUnavailable, service.Like(MatchId, Me).Status);
    }

    /// <summary>停權到期的人回到牌堆之後，右滑要正常走完。</summary>
    [Fact]
    public void ExpiredSuspensionCanBeLiked()
    {
        var (service, _) = Build(
            nameof(ExpiredSuspensionCanBeLiked), AccountStatus.SUSPENDED, TestWorld.SuspensionOver);

        Assert.Equal(LikeStatus.Ok, service.Like(MatchId, Me).Status);
    }

    /// <summary>
    /// 這是整件事真正的傷害：對方已經不在，但反向那列早就右滑過，於是配對被
    /// 判定成互相、兩列都被標記、通知送出去，而聊天室開不起來。擋在前面之後，
    /// 資料庫必須一個字都沒動。
    /// </summary>
    [Fact]
    public void UnavailableCandidateLeavesNoHalfFinishedMatch()
    {
        var (service, db) = Build(
            nameof(UnavailableCandidateLeavesNoHalfFinishedMatch), AccountStatus.DELETED, null,
            reciprocalAlreadyLiked: true);

        var outcome = service.Like(MatchId, Me);

        Assert.Equal(LikeStatus.CandidateUnavailable, outcome.Status);

        var mine = db.Matches.Single(x => x.Id == MatchId);
        Assert.False(mine.UserLiked);
        Assert.False(mine.MatchedUserLiked);

        // 反向那列也不能被動到——它是對方當初真實的表態
        var theirs = db.Matches.Single(x => x.UserId == Other && x.MatchedUserId == Me);
        Assert.False(theirs.MatchedUserLiked);
    }

    [Fact]
    public void SomeoneElsesMatchIsStillNotFound()
    {
        var (service, _) = Build(nameof(SomeoneElsesMatchIsStillNotFound), AccountStatus.ACTIVE, null);

        Assert.Equal(LikeStatus.NotFound, service.Like(MatchId, "user_stranger").Status);
    }

    [Fact]
    public void UnknownMatchIsNotFound()
    {
        var (service, _) = Build(nameof(UnknownMatchIsNotFound), AccountStatus.ACTIVE, null);

        Assert.Equal(LikeStatus.NotFound, service.Like("match_does_not_exist", Me).Status);
    }

    /// <summary>
    /// 「我」和一位候選，外加一筆已經發到手上的配對卡。
    /// reciprocalAlreadyLiked 模擬對方先右滑過我——那是會走到「互相同頻」的情境。
    /// </summary>
    private static (MatchService service, AppDbContext db) Build(
        string databaseName,
        AccountStatus candidateStatus,
        DateTimeOffset? suspendedUntil,
        bool reciprocalAlreadyLiked = false)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.AddRange(
            TestWorld.User(Me),
            TestWorld.User(Other, candidateStatus, suspendedUntil));

        db.Matches.Add(NewMatch(MatchId, Me, Other, userLiked: false));
        if (reciprocalAlreadyLiked)
        {
            db.Matches.Add(NewMatch("match_other_me", Other, Me, userLiked: true));
        }
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);

        return (new MatchService(TestWorld.Storage(), services.BuildServiceProvider()), db);
    }

    private static MatchRecordEntity NewMatch(string id, string userId, string matchedUserId, bool userLiked)
        => new()
        {
            Id = id,
            UserId = userId,
            MatchedUserId = matchedUserId,
            MatchScore = 50,
            // System.IO 也有一個 MatchType，不寫全名會模稜兩可
            MatchType = SameDaySocialApp.Domain.Enums.MatchType.SAME_EVENT.ToString(),
            SharedTags = [],
            Reason = "同樣被工作壓著",
            Icebreaker = "今天也辛苦了",
            UserLiked = userLiked
        };
}
