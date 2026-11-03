using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 配對候選池的帳號狀態過濾。
///
/// 為什麼要有這組測試：
/// 帳號處置（停權／封鎖／註銷）不會回頭刪掉 today_entries，所以只要這個人
/// 曾經發過一篇非 PRIVATE 的今天，他的記錄就一直躺在那裡。候選池如果不看
/// users.status，被註銷的帳號會繼續出現在別人的牌堆——對方右滑配得成功、
/// 開得了聊天室、通知照送，收件的卻是一個已經不存在的人。
///
/// 停權是另一個方向的坑：它是「暫時」的，但把 users.status 寫回 ACTIVE 的
/// AdminUserService.RestoreExpiredSuspensions 只在後台開啟會員列表時才跑，
/// 沒有排程。所以過濾時必須自己看 suspended_until，否則沒人開後台就等於永久停權。
/// </summary>
public sealed class MatchServiceAccountStatusTests
{
    private const string Me = "user_me";
    private const string Other = "user_other";

    /// <summary>
    /// 先確認這組測試真的發得出牌。少了這一個，其他「不應出現」的斷言
    /// 就算是因為牌堆整個壞掉而空的，也會一樣綠。
    /// </summary>
    [Fact]
    public void ActiveCandidateIsInDeck()
    {
        var service = Build(nameof(ActiveCandidateIsInDeck), Candidate(AccountStatus.ACTIVE));

        var deck = service.GetTodayMatches(Me);

        Assert.Contains(deck, x => x.MatchedUserId == Other);
    }

    [Fact]
    public void DeletedAccountIsNotInDeck()
    {
        var service = Build(nameof(DeletedAccountIsNotInDeck), Candidate(AccountStatus.DELETED));

        var deck = service.GetTodayMatches(Me);

        Assert.DoesNotContain(deck, x => x.MatchedUserId == Other);
    }

    [Fact]
    public void BannedAccountIsNotInDeck()
    {
        var service = Build(nameof(BannedAccountIsNotInDeck), Candidate(AccountStatus.BANNED));

        var deck = service.GetTodayMatches(Me);

        Assert.DoesNotContain(deck, x => x.MatchedUserId == Other);
    }

    /// <summary>停權還沒到期：擋住。這是下一個測試的對照組。</summary>
    [Fact]
    public void RunningSuspensionIsNotInDeck()
    {
        var service = Build(
            nameof(RunningSuspensionIsNotInDeck),
            Candidate(AccountStatus.SUSPENDED, DateTimeOffset.UtcNow.AddDays(3)));

        var deck = service.GetTodayMatches(Me);

        Assert.DoesNotContain(deck, x => x.MatchedUserId == Other);
    }

    /// <summary>
    /// 停權到期：回到牌堆。
    /// 這裡刻意讓 users.status 停在 SUSPENDED——真實資料就長這樣，
    /// 因為把它寫回 ACTIVE 的那段程式要等有人開後台才會跑。
    /// </summary>
    [Fact]
    public void ExpiredSuspensionReturnsToDeck()
    {
        var service = Build(
            nameof(ExpiredSuspensionReturnsToDeck),
            Candidate(AccountStatus.SUSPENDED, DateTimeOffset.UtcNow.AddMinutes(-1)));

        var deck = service.GetTodayMatches(Me);

        Assert.Contains(deck, x => x.MatchedUserId == Other);
    }

    /// <summary>
    /// 沒有期限的停權（後台選了「無限期」）不會因為時間過去而恢復。
    /// </summary>
    [Fact]
    public void SuspensionWithoutDeadlineIsNotInDeck()
    {
        var service = Build(
            nameof(SuspensionWithoutDeadlineIsNotInDeck),
            Candidate(AccountStatus.SUSPENDED, null));

        var deck = service.GetTodayMatches(Me);

        Assert.DoesNotContain(deck, x => x.MatchedUserId == Other);
    }

    // ── 測試資料 ──

    private static UserRecord Candidate(AccountStatus status, DateTimeOffset? suspendedUntil = null)
        => NewUser(Other, status, suspendedUntil);

    private static UserRecord NewUser(string id, AccountStatus status, DateTimeOffset? suspendedUntil)
        => new()
        {
            Id = id,
            Nickname = id,
            Status = status.ToString(),
            SuspendedUntil = suspendedUntil
        };

    /// <summary>
    /// 兩人同為 WORK_STRESS + TIRED，分數必定大於零；牌堆空不空只取決於過濾。
    /// </summary>
    private static TodayEntryRecord NewEntry(string userId)
        => new()
        {
            Id = $"entry_{userId}",
            UserId = userId,
            Content = "今天被會議塞滿了",
            EventType = EventType.WORK_STRESS.ToString(),
            EmotionTags = [EmotionTag.TIRED.ToString()],
            ValueTags = [],
            InterestTags = [],
            ResponseMode = SameDaySocialApp.Domain.Enums.ResponseMode.JUST_LISTEN.ToString(),
            Visibility = Visibility.MATCH_ONLY.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        };

    /// <summary>
    /// 一個只有「我」和一位候選的最小資料庫。
    /// 每個測試各自一份 InMemory 資料庫，彼此不互相汙染。
    /// </summary>
    private static MatchService Build(string databaseName, UserRecord candidate)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var db = new AppDbContext(options);
        db.Users.AddRange(NewUser(Me, AccountStatus.ACTIVE, null), candidate);
        db.TodayEntries.AddRange(NewEntry(Me), NewEntry(candidate.Id));
        db.SaveChanges();

        // MatchService 自己從容器拿 AppDbContext；拿得到就走資料庫路徑，
        // NotificationService / ILogger 沒註冊，服務本來就允許為 null。
        var services = new ServiceCollection();
        services.AddSingleton(db);

        return new MatchService(new JsonStorageService(new TestEnvironment()), services.BuildServiceProvider());
    }

    /// <summary>
    /// JsonStorageService 的建構子要 IWebHostEnvironment。資料庫路徑下它一行都不會被呼叫，
    /// 但建構子會建 Data 目錄，所以指到各自的暫存資料夾，不碰專案目錄。
    /// </summary>
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "backend.Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } =
            Path.Combine(Path.GetTempPath(), "tongpin-tests", Guid.NewGuid().ToString("N"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
