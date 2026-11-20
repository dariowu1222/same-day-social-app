using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 別人看到的是「年齡＋星座」，不是出生年月日。
///
/// 以前配對卡與個人檔案的回應都帶著完整的生日，因為星座是前端拿月日算的。
/// 生日那時是選填，大多數人沒有；現在註冊就必填，等於每個人的出生年月日會跟著
/// 每一張發出去的卡片送到別人手上。卡片上顯示的從來只有年齡與星座，
/// 所以改由後端算好這兩樣，日期本身不離開伺服器。
///
/// 個人檔案那一支（GET /api/profile/{id}）的測試在 ProfileVisibilityTests；
/// 這裡釘配對卡的回應，以及星座本身算得對不對。
/// </summary>
public sealed class BirthdayPrivacyTests
{
    private const string Me = "user_me";

    // ── 配對卡 ──

    [Fact]
    public void AMatchCardCarriesAgeAndZodiacButNotTheBirthday()
    {
        var birthday = new DateOnly(1994, 8, 9);
        var card = Deck(nameof(AMatchCardCarriesAgeAndZodiacButNotTheBirthday), Person("leo", birthday)).Single();

        Assert.Equal(AgeRules.AgeOf(birthday), card.Age);
        Assert.Equal("獅子座", card.Zodiac);

        // 送出去的 JSON 裡不能有生日：沒有這個欄位，也沒有哪個值是那個日期或那一年。
        // （比對的是完整的值而不是任意子字串——配對編號是亂數，剛好含有「1994」四個字不代表洩漏。）
        var json = JsonSerializer.Serialize(card, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("birthday", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1994-08-09", json);
        Assert.DoesNotContain("\"1994\"", json);
        Assert.DoesNotContain(":1994", json);
    }

    /// <summary>沒有生日的人（還沒更新、還沒補填的舊帳號）：年齡與星座是空的，卡片照樣發。</summary>
    [Fact]
    public void ACardForSomeoneWithoutABirthdayHasNoAgeOrZodiac()
    {
        var card = Deck(nameof(ACardForSomeoneWithoutABirthdayHasNoAgeOrZodiac), Person("unknown", birthday: null)).Single();

        Assert.Null(card.Age);
        Assert.Null(card.Zodiac);
    }

    // ── 星座 ──

    /// <summary>每個星座的第一天與最後一天，以及跨年的摩羯座。</summary>
    [Theory]
    [InlineData(1, 19, "摩羯座")]
    [InlineData(1, 20, "水瓶座")]
    [InlineData(2, 18, "水瓶座")]
    [InlineData(2, 19, "雙魚座")]
    [InlineData(2, 29, "雙魚座")]
    [InlineData(3, 20, "雙魚座")]
    [InlineData(3, 21, "牡羊座")]
    [InlineData(4, 19, "牡羊座")]
    [InlineData(4, 20, "金牛座")]
    [InlineData(5, 20, "金牛座")]
    [InlineData(5, 21, "雙子座")]
    [InlineData(6, 20, "雙子座")]
    [InlineData(6, 21, "巨蟹座")]
    [InlineData(7, 22, "巨蟹座")]
    [InlineData(7, 23, "獅子座")]
    [InlineData(8, 22, "獅子座")]
    [InlineData(8, 23, "處女座")]
    [InlineData(9, 22, "處女座")]
    [InlineData(9, 23, "天秤座")]
    [InlineData(10, 22, "天秤座")]
    [InlineData(10, 23, "天蠍座")]
    [InlineData(11, 21, "天蠍座")]
    [InlineData(11, 22, "射手座")]
    [InlineData(12, 21, "射手座")]
    [InlineData(12, 22, "摩羯座")]
    [InlineData(12, 31, "摩羯座")]
    [InlineData(1, 1, "摩羯座")]
    public void ZodiacBoundaries(int month, int day, string expected)
    {
        Assert.Equal(expected, AgeRules.ZodiacOf(new DateOnly(2000, month, day)));
    }

    /// <summary>
    /// 後端的星座和前端的 getZodiac 逐日對得上。
    ///
    /// 星座現在有三份：這裡的 AgeRules.ZodiacOf（別人看到的）、profileUtils.ts（「我的」頁
    /// 與自己的卡片預覽）、userDisplay.ts（聊天室，後端還沒更新時的退路）。
    /// 三份各寫各的，哪一份的分界日改了一天，同一個人在自己眼中和別人眼中就是不同的星座。
    /// 所以直接讀前端那兩個檔的規則，把一整個閏年的每一天都算一次來比。
    /// </summary>
    [Theory]
    [InlineData("frontend/src/features/profile/profileUtils.ts")]
    [InlineData("frontend/src/shared/lib/userDisplay.ts")]
    public void ZodiacAgreesWithTheFrontendOnEveryDayOfTheYear(string relativePath)
    {
        var frontend = FrontendZodiacRule(File.ReadAllText(RepoFile(relativePath)));

        var disagreements = new List<string>();
        for (var date = new DateOnly(2000, 1, 1); date.Year == 2000; date = date.AddDays(1))
        {
            var mine = AgeRules.ZodiacOf(date);
            var theirs = frontend(date.Month * 100 + date.Day);
            if (mine != theirs) disagreements.Add($"{date:MM-dd} 後端 {mine}／前端 {theirs}");
        }

        Assert.True(disagreements.Count == 0, $"{relativePath} 與後端的星座對不上：{string.Join("、", disagreements.Take(8))}");
    }

    /// <summary>
    /// 把前端 getZodiac 裡的規則讀出來，變成一個可以拿日期去問的函式。
    /// 前端的寫法是一串 <c>if (md >= 120 &amp;&amp; md &lt;= 218) return '水瓶座'</c>，最後 <c>return '摩羯座'</c>。
    /// </summary>
    private static Func<int, string> FrontendZodiacRule(string source)
    {
        var body = Regex.Match(source, @"export function getZodiac\(.*?\n\}", RegexOptions.Singleline);
        Assert.True(body.Success, "找不到 getZodiac——改名或搬走了的話，這條測試要跟著改");

        var ranges = Regex.Matches(body.Value, @"md >= (\d+) && md <= (\d+)\) return '([^']+)'")
            .Select(m => (From: int.Parse(m.Groups[1].Value), To: int.Parse(m.Groups[2].Value), Name: m.Groups[3].Value))
            .ToList();
        Assert.Equal(11, ranges.Count);

        var fallback = Regex.Matches(body.Value, @"\n\s*return '([^']+)'").Last().Groups[1].Value;

        return monthDay => ranges.Where(r => monthDay >= r.From && monthDay <= r.To).Select(r => r.Name).FirstOrDefault() ?? fallback;
    }

    /// <summary>用這個測試檔自己的路徑回推 repo 根目錄（測試輸出常被導到別處，從執行目錄往上走到不了）。</summary>
    private static string RepoFile(string relative, [CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)!)!, relative);

    // ── 測試資料 ──

    private static UserRecord Person(string id, DateOnly? birthday)
        => new()
        {
            Id = id,
            Nickname = id,
            Status = AccountStatus.ACTIVE.ToString(),
            Gender = "女",
            Birthday = birthday,
        };

    private static List<MatchResponse> Deck(string databaseName, params UserRecord[] others)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.Add(Person(Me, new DateOnly(1990, 1, 1)));
        db.Users.AddRange(others);
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        return new MatchService(TestWorld.Storage(), services.BuildServiceProvider()).GetTodayMatches(Me);
    }
}
