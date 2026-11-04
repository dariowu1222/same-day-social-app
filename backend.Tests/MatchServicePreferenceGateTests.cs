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
/// 候選資格的兩道硬界線，以及「沒發今日」的人怎麼算分。
///
/// 為什麼要有這組測試：
/// 這三件事湊在一起會產生一個反直覺的結果——資料愈空白的帳號愈佔便宜。
/// 性別與生日沒填會讓兩道篩選各自整段跳過；而沒發今日的人以前會退回
/// users.response_preference，那個欄位全站沒有介面能設定，值一律是註冊寫死的
/// JUST_LISTEN，又剛好是計分表裡唯一跟誰都相容的值。三者相加，一個登入完
/// 什麼都沒做的帳號會排在認真填完資料、而且今天真的寫了心情的人前面。
///
/// 這幾條都不是靠讀程式碼看得出來的，要有資料跑過才會現形，所以用測試釘住。
/// </summary>
public sealed class MatchServicePreferenceGateTests
{
    private const string Me = "user_me";

    // ── 硬界線 ──

    /// <summary>
    /// 本人指定了想認識的性別，候選卻沒填性別——不發這張卡。
    /// 以前是「比不出來就放行」，等於沒填的人反而不受限制。
    /// </summary>
    [Fact]
    public void CandidateWithoutGenderIsExcludedWhenIStatedOne()
    {
        var deck = Build(nameof(CandidateWithoutGenderIsExcludedWhenIStatedOne),
            me: Picky(Me, lookingFor: "女生"),
            others: [Person("hasGender", gender: "女"), Person("blank", gender: null)])
            .GetTodayMatches(Me);

        Assert.Equal(["hasGender"], deck.Select(x => x.MatchedUserId));
    }

    /// <summary>本人設了年齡範圍，候選沒填生日——同樣不發。</summary>
    [Fact]
    public void CandidateWithoutBirthdayIsExcludedWhenIStatedAnAgeRange()
    {
        var deck = Build(nameof(CandidateWithoutBirthdayIsExcludedWhenIStatedAnAgeRange),
            me: Picky(Me, ageMin: 20, ageMax: 40),
            others: [Person("hasBirthday", age: 30), Person("blank", age: null)])
            .GetTodayMatches(Me);

        Assert.Equal(["hasBirthday"], deck.Select(x => x.MatchedUserId));
    }

    /// <summary>
    /// 本人什麼都沒指定時，候選的缺漏不該害他被濾掉。
    /// 少了這條，上面兩個測試會變成「資料不全一律封殺」，把真實用戶整批濾光。
    /// </summary>
    [Fact]
    public void MissingDataIsFineWhenIStatedNoPreference()
    {
        var deck = Build(nameof(MissingDataIsFineWhenIStatedNoPreference),
            me: Person(Me),
            others: [Person("blank", gender: null, age: null)])
            .GetTodayMatches(Me);

        Assert.Equal(["blank"], deck.Select(x => x.MatchedUserId));
    }

    /// <summary>「不限」跟沒填一樣，不該把沒填性別的人擋掉。</summary>
    [Fact]
    public void AnyGenderPreferenceDoesNotExcludeCandidatesWithoutGender()
    {
        var deck = Build(nameof(AnyGenderPreferenceDoesNotExcludeCandidatesWithoutGender),
            me: Picky(Me, lookingFor: "不限"),
            others: [Person("blank", gender: null)])
            .GetTodayMatches(Me);

        Assert.Equal(["blank"], deck.Select(x => x.MatchedUserId));
    }

    // ── 沒發今日的人 ──

    /// <summary>
    /// 沒發今日的人不該拿到「聊天節奏合」，也不該因此排在有發今日的人前面。
    /// 回應方式講的是「我今天想要什麼」，沒發今日就是沒說。
    /// </summary>
    [Fact]
    public void UserWithoutTodayEntryGetsNoResponseCompatibility()
    {
        var service = Build(nameof(UserWithoutTodayEntryGetsNoResponseCompatibility),
            me: Person(Me), others: [Person("silent"), Person("posted")],
            entries:
            [
                Entry(Me, EventType.FAMILY, [], ResponseMode.COMFORT_ME),
                Entry("posted", EventType.FAMILY, [], ResponseMode.JUST_LISTEN),
            ]);

        var deck = service.GetTodayMatches(Me);

        Assert.DoesNotContain("RESPONSE_COMPATIBLE",
            deck.Single(x => x.MatchedUserId == "silent").SharedTags);
        Assert.Equal("posted", deck[0].MatchedUserId);
    }

    // ── SHARE_GOOD_NEWS ──

    /// <summary>
    /// 兩個都在分享好事的人互相契合。
    /// 這張卡以前存的是 DISTRACT_ME，靠 a == b 碰巧也拿 15 分；
    /// 換成語意正確的值之後，這個行為要維持住。
    /// </summary>
    [Fact]
    public void TwoPeopleSharingGoodNewsAreCompatible()
    {
        var service = Build(nameof(TwoPeopleSharingGoodNewsAreCompatible),
            me: Person(Me), others: [Person("alsoGlad")],
            entries:
            [
                Entry(Me, EventType.SMALL_HAPPINESS, [], ResponseMode.SHARE_GOOD_NEWS),
                Entry("alsoGlad", EventType.SMALL_HAPPINESS, [], ResponseMode.SHARE_GOOD_NEWS),
            ]);

        Assert.Contains("RESPONSE_COMPATIBLE", service.GetTodayMatches(Me)[0].SharedTags);
    }

    /// <summary>
    /// 今天很低落的人，不該因為對方在分享好消息就被標成「聊天節奏合」。
    /// 心情好的人仍然進得了牌堆——走的是 BuildDeck 的保留席，有名額上限，
    /// 不是靠回應方式拿滿分擠進來。
    /// </summary>
    [Fact]
    public void GoodNewsIsNotMarkedCompatibleWithSomeoneWhoNeedsComfort()
    {
        var service = Build(nameof(GoodNewsIsNotMarkedCompatibleWithSomeoneWhoNeedsComfort),
            me: Person(Me), others: [Person("glad")],
            entries:
            [
                Entry(Me, EventType.WORK_STRESS, [EmotionTag.TIRED.ToString()], ResponseMode.COMFORT_ME),
                Entry("glad", EventType.SMALL_HAPPINESS, [], ResponseMode.SHARE_GOOD_NEWS),
            ]);

        Assert.DoesNotContain("RESPONSE_COMPATIBLE", service.GetTodayMatches(Me)[0].SharedTags);
    }

    // ── 測試資料 ──

    private static UserRecord Person(
        string id, string? gender = "女", int? age = 30)
        => new()
        {
            Id = id,
            Nickname = id,
            Status = AccountStatus.ACTIVE.ToString(),
            Gender = gender,
            Birthday = age == null
                ? null
                : DateOnly.FromDateTime(DateTime.Today.AddYears(-age.Value).AddDays(-1)),
        };

    /// <summary>有表明偏好的本人。沒指定的項目維持 null，代表「沒意見」。</summary>
    private static UserRecord Picky(
        string id, string? lookingFor = null, int? ageMin = null, int? ageMax = null)
    {
        var user = Person(id);
        user.LookingFor = lookingFor;
        user.AgeMin = ageMin;
        user.AgeMax = ageMax;
        return user;
    }

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
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static MatchService Build(
        string databaseName,
        UserRecord me,
        List<UserRecord> others,
        List<TodayEntryRecord>? entries = null)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName).Options);

        db.Users.Add(me);
        db.Users.AddRange(others);
        if (entries != null) db.TodayEntries.AddRange(entries);
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
