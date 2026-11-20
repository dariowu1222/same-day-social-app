using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 地區偏好真的會篩人。
///
/// 為什麼要有這組測試：
/// 「距離偏好」與「偏好縣市」從 onboarding 第一版就讓人填，但一直只有存檔——
/// 設了台北市照樣滑到高雄。收了偏好卻不照做，使用者得到的結論是「偏好設定沒有用」，
/// 之後連真的會生效的那幾項也不信了。
///
/// 這組釘的除了「會篩」之外，還有兩個不直覺的邊界：
/// 我自己沒填所在縣市時距離不篩（沒有起點可算，而既有帳號一開始全都是空的）；
/// 對方沒填所在縣市時則照硬界線的規則擋。兩邊的處理刻意不對稱，寫反了不會有任何錯誤訊息，
/// 只會變成牌堆莫名其妙是空的，或莫名其妙全都過。
/// </summary>
public sealed class MatchServiceAreaGateTests
{
    private const string Me = "user_me";

    // ── 依縣市 ──

    [Fact]
    public void PreferredAreaKeepsOnlyCandidatesLivingThere()
    {
        var deck = Deck(nameof(PreferredAreaKeepsOnlyCandidatesLivingThere),
            me: Person(Me, preferredArea: "台北市"),
            Person("taipei", location: "台北市"),
            Person("kaohsiung", location: "高雄市"));

        Assert.Equal(["taipei"], deck);
    }

    /// <summary>我指定了縣市，對方沒填所在地——沒有可比對的欄位，不發。</summary>
    [Fact]
    public void PreferredAreaExcludesCandidatesWithoutALocation()
    {
        var deck = Deck(nameof(PreferredAreaExcludesCandidatesWithoutALocation),
            me: Person(Me, preferredArea: "台北市"),
            Person("taipei", location: "台北市"),
            Person("blank", location: null));

        Assert.Equal(["taipei"], deck);
    }

    /// <summary>依縣市不需要知道我自己在哪：我人在高雄、想認識台北的人，是合理的選擇。</summary>
    [Fact]
    public void PreferredAreaDoesNotNeedMyOwnLocation()
    {
        var deck = Deck(nameof(PreferredAreaDoesNotNeedMyOwnLocation),
            me: Person(Me, location: null, preferredArea: "台北市"),
            Person("taipei", location: "台北市"));

        Assert.Equal(["taipei"], deck);
    }

    // ── 依距離 ──

    /// <summary>
    /// 台北市到新北市（板橋）約 10 公里、到桃園約 27 公里、到高雄約 300 公里。
    /// 設 30 公里應該留下前三者（含同縣市），濾掉高雄。
    /// </summary>
    [Fact]
    public void DistanceKeepsCandidatesWithinRange()
    {
        var deck = Deck(nameof(DistanceKeepsCandidatesWithinRange),
            me: Person(Me, location: "台北市", distanceKm: 30),
            Person("sameCity", location: "台北市"),
            Person("newTaipei", location: "新北市"),
            Person("taoyuan", location: "桃園市"),
            Person("kaohsiung", location: "高雄市"));

        Assert.Equal(["newTaipei", "sameCity", "taoyuan"], deck);
    }

    /// <summary>同縣市算 0 公里：距離設得再小，同縣市的人也一定留著。</summary>
    [Fact]
    public void SameCountyAlwaysPassesTheDistanceRule()
    {
        var deck = Deck(nameof(SameCountyAlwaysPassesTheDistanceRule),
            me: Person(Me, location: "台北市", distanceKm: 1),
            Person("sameCity", location: "台北市"),
            Person("newTaipei", location: "新北市"));

        Assert.Equal(["sameCity"], deck);
    }

    [Fact]
    public void DistanceExcludesCandidatesWithoutALocation()
    {
        var deck = Deck(nameof(DistanceExcludesCandidatesWithoutALocation),
            me: Person(Me, location: "台北市", distanceKm: 30),
            Person("sameCity", location: "台北市"),
            Person("blank", location: null));

        Assert.Equal(["sameCity"], deck);
    }

    /// <summary>
    /// 我設了距離、但沒填自己的所在縣市：沒有起點，不篩。
    /// 所在縣市是跟著這次修改才開始收的，既有帳號一開始全部是空的——
    /// 這時把所有人濾掉，等於懲罰一個還沒被問過「你在哪」的人。
    /// </summary>
    [Fact]
    public void DistanceIsNotAppliedUntilIStateWhereIAm()
    {
        var deck = Deck(nameof(DistanceIsNotAppliedUntilIStateWhereIAm),
            me: Person(Me, location: null, distanceKm: 30),
            Person("taipei", location: "台北市"),
            Person("kaohsiung", location: "高雄市"),
            Person("blank", location: null));

        Assert.Equal(["blank", "kaohsiung", "taipei"], deck);
    }

    // ── 兩者之間 ──

    /// <summary>
    /// 兩欄同時有值（寫入已經互斥，只剩種子資料會這樣）：以縣市為準。
    /// 這裡距離 30 公里本來會放行新北市，而縣市指定了台北市，所以新北市不該出現。
    /// </summary>
    [Fact]
    public void PreferredAreaWinsWhenBothAreSet()
    {
        var deck = Deck(nameof(PreferredAreaWinsWhenBothAreSet),
            me: Person(Me, location: "台北市", preferredArea: "台北市", distanceKm: 30),
            Person("sameCity", location: "台北市"),
            Person("newTaipei", location: "新北市"));

        Assert.Equal(["sameCity"], deck);
    }

    /// <summary>沒表明任何地區偏好：一切照舊，沒填所在地的人也照發。</summary>
    [Fact]
    public void NoAreaPreferenceMeansNoAreaFiltering()
    {
        var deck = Deck(nameof(NoAreaPreferenceMeansNoAreaFiltering),
            me: Person(Me, location: "台北市"),
            Person("kaohsiung", location: "高雄市"),
            Person("blank", location: null));

        Assert.Equal(["blank", "kaohsiung"], deck);
    }

    /// <summary>
    /// 偏好縣市裡是清單外的字串（這欄以前是自由文字）：當成沒指定，不要把整副牌濾光。
    /// </summary>
    [Fact]
    public void UnknownPreferredAreaIsTreatedAsNotStated()
    {
        var deck = Deck(nameof(UnknownPreferredAreaIsTreatedAsNotStated),
            me: Person(Me, preferredArea: "北部"),
            Person("taipei", location: "台北市"),
            Person("kaohsiung", location: "高雄市"));

        Assert.Equal(["kaohsiung", "taipei"], deck);
    }

    // ── 測試資料 ──

    private static UserRecord Person(
        string id, string? location = null, string? preferredArea = null, int? distanceKm = null)
    {
        var user = TestWorld.User(id);
        user.LocationArea = location;
        user.PreferredArea = preferredArea;
        user.DistanceKm = distanceKm;
        return user;
    }

    /// <summary>發給我的那副牌上有誰。排序過再比，這組測試不關心先後。</summary>
    private static List<string> Deck(string databaseName, UserRecord me, params UserRecord[] others)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.Add(me);
        db.Users.AddRange(others);
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);

        return new MatchService(TestWorld.Storage(), services.BuildServiceProvider())
            .GetTodayMatches(Me)
            .Select(x => x.MatchedUserId)
            .Order()
            .ToList();
    }
}
