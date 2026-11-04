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
/// 牌堆的情緒配比，以及回應方式的計分。
///
/// 為什麼要有這組測試：
/// 情緒交集每個 +10、回應方式最高 +15，而今日頁最負面的兩張卡（有點累、
/// 有點委屈）都對應 COMFORT_ME。兩個低落的人會同時吃到這兩項，被推到牌堆
/// 最前面——整副牌於是變成一面負面的鏡子。這兩條規則都是刻意加的，
/// 沒有測試的話，日後任何人調計分都可能默默把它們抵銷掉。
///
/// 這裡斷言的是「行為」不是「分數」：GetTodayMatches 回傳前會把分數
/// min-max 正規化到 80~98，原始分看不到，但排序與標籤看得到。
/// </summary>
public sealed class MatchServiceMoodBalanceTests
{
    private const string Me = "user_me";

    /// <summary>
    /// 情緒吃力的使用者，牌堆裡要有「看起來還有餘裕」的人。
    /// 保留席的候選分數是 0、同溫層候選是 55，不保留的話一張都擠不進來。
    /// </summary>
    [Fact]
    public void LowMoodDeckReservesSlotsForSteadyPeople()
    {
        var service = Build(nameof(LowMoodDeckReservesSlotsForSteadyPeople),
            me: Heavy(Me),
            others: Many("low", 12, Heavy).Concat(Many("steady", 3, Steady)).ToList());

        var deck = service.GetTodayMatches(Me);

        Assert.Equal(10, deck.Count);
        Assert.Equal(3, deck.Count(x => x.MatchedUserId.StartsWith("steady")));
    }

    /// <summary>
    /// 湊不到有餘裕的人就讓位，牌堆不會因此變短。
    /// 少了這個，上面那個測試就算是因為「保留席硬是空出來」而綠的也看不出來。
    /// </summary>
    [Fact]
    public void LowMoodDeckStaysFullWhenNobodyIsSteady()
    {
        var service = Build(nameof(LowMoodDeckStaysFullWhenNobodyIsSteady),
            me: Heavy(Me),
            others: Many("low", 12, Heavy));

        var deck = service.GetTodayMatches(Me);

        Assert.Equal(10, deck.Count);
    }

    /// <summary>
    /// 使用者今天不吃力時不保留——牌堆就是照分數取前十。
    /// 保留席是給低谷的人的透氣口，不是一條永遠生效的規則。
    /// </summary>
    [Fact]
    public void SteadyUserGetsPlainTopTen()
    {
        var service = Build(nameof(SteadyUserGetsPlainTopTen),
            me: Calm(Me),
            others: Many("high", 12, Calm).Concat(Many("steady", 3, Unrelated)).ToList());

        var deck = service.GetTodayMatches(Me);

        Assert.Equal(10, deck.Count);
        Assert.DoesNotContain(deck, x => x.MatchedUserId.StartsWith("steady"));
    }

    /// <summary>
    /// 兩個都想被安慰的人，分數要低於「一個想被安慰、一個低需求」。
    /// 五個 ResponseMode 全部都是「我希望對方怎麼對我」，沒有供給面，
    /// 所以兩邊都在要的時候，沒有人站在給的那一邊。
    /// </summary>
    [Fact]
    public void MutualNeedScoresLowerThanOneSidedNeed()
    {
        var service = Build(nameof(MutualNeedScoresLowerThanOneSidedNeed),
            me: Entry(Me, EventType.FAMILY, [], ResponseMode.COMFORT_ME),
            others:
            [
                Entry("alsoNeeds", EventType.FAMILY, [], ResponseMode.COMFORT_ME),
                Entry("lowDemand", EventType.FAMILY, [], ResponseMode.JUST_LISTEN),
            ]);

        var deck = service.GetTodayMatches(Me);

        Assert.Equal("lowDemand", deck[0].MatchedUserId);
    }

    /// <summary>
    /// 「聊天節奏合」這個標籤只在真的相容時才貼。
    /// 兩個都在要的時候還貼上去，是在卡片上騙人。
    /// </summary>
    [Fact]
    public void MutualNeedDoesNotGetCompatibleTag()
    {
        var service = Build(nameof(MutualNeedDoesNotGetCompatibleTag),
            me: Entry(Me, EventType.FAMILY, [], ResponseMode.COMFORT_ME),
            others:
            [
                Entry("alsoNeeds", EventType.FAMILY, [], ResponseMode.COMFORT_ME),
                Entry("lowDemand", EventType.FAMILY, [], ResponseMode.JUST_LISTEN),
            ]);

        var deck = service.GetTodayMatches(Me);

        Assert.DoesNotContain("RESPONSE_COMPATIBLE",
            deck.Single(x => x.MatchedUserId == "alsoNeeds").SharedTags);
        Assert.Contains("RESPONSE_COMPATIBLE",
            deck.Single(x => x.MatchedUserId == "lowDemand").SharedTags);
    }

    // ── 測試資料 ──

    /// <summary>今天很吃力，而且自己也在要安慰。保留席不會選到這種人。</summary>
    private static TodayEntryRecord Heavy(string id)
        => Entry(id, EventType.WORK_STRESS,
            [EmotionTag.TIRED.ToString(), EmotionTag.WRONGED.ToString()], ResponseMode.COMFORT_ME);

    /// <summary>今天平穩、沒有在要什麼。這才是保留席要的人，刻意不用 HAPPY。</summary>
    private static TodayEntryRecord Steady(string id)
        => Entry(id, EventType.SMALL_HAPPINESS,
            [EmotionTag.CALM.ToString()], ResponseMode.DISTRACT_ME);

    private static TodayEntryRecord Calm(string id)
        => Entry(id, EventType.FAMILY, [EmotionTag.CALM.ToString()], ResponseMode.COMFORT_ME);

    /// <summary>跟 Calm 完全不重疊，分數必定敬陪末座。</summary>
    private static TodayEntryRecord Unrelated(string id)
        => Entry(id, EventType.MONEY, [EmotionTag.EXCITED.ToString()], ResponseMode.DISTRACT_ME);

    private static List<TodayEntryRecord> Many(
        string prefix, int count, Func<string, TodayEntryRecord> make)
        => Enumerable.Range(0, count).Select(i => make($"{prefix}_{i}")).ToList();

    private static TodayEntryRecord Entry(
        string userId, EventType eventType, string[] emotions, ResponseMode response)
        => new()
        {
            Id = $"entry_{userId}",
            UserId = userId,
            Content = "測試用",
            EventType = eventType.ToString(),
            EmotionTags = emotions,
            ValueTags = [],
            InterestTags = [],
            ResponseMode = response.ToString(),
            Visibility = Visibility.MATCH_ONLY.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static UserRecord NewUser(string id)
        => new() { Id = id, Nickname = id, Status = AccountStatus.ACTIVE.ToString() };

    private static MatchService Build(
        string databaseName, TodayEntryRecord me, List<TodayEntryRecord> others)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName).Options);

        db.Users.Add(NewUser(me.UserId));
        db.Users.AddRange(others.Select(x => NewUser(x.UserId)));
        db.TodayEntries.Add(me);
        db.TodayEntries.AddRange(others);
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        return new MatchService(new JsonStorageService(new TestEnvironment()), services.BuildServiceProvider());
    }

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
