using Microsoft.EntityFrameworkCore;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Infrastructure.Persistence;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 把查詢交給真的 PostgreSQL 驅動翻成 SQL，確認翻得出來。
///
/// 為什麼需要：其他測試用的是記憶體資料庫，它什麼 LINQ 都跑得動——
/// 群組之後再 join、子查詢裡的 Any、陣列欄位的投影，在那邊全部會過。
/// 真的資料庫不一定：翻不出來的查詢要到線上第一次被打到才會丟例外，
/// 而配對牌堆是開 App 之後的第一個請求。
///
/// 這裡不連線。ToQueryString 只做「LINQ → SQL」這一步，連線字串是假的也沒關係。
/// 所以它驗的是翻不翻得出來、翻成什麼樣子，不是跑起來快不快。
/// </summary>
public sealed class QueryTranslationTests
{
    private static AppDbContext Postgres()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=nobody")
            .Options);

    /// <summary>
    /// 候選池：暫停、已表態、封鎖都要是資料庫裡的 NOT EXISTS，而且只拿算分用的欄位——
    /// 照片網址陣列與自我介紹不能出現在這句裡，那正是這次要省掉的東西。
    /// </summary>
    [Fact]
    public void TheCandidatePoolIsFilteredAndTrimmedByTheDatabase()
    {
        using var db = Postgres();

        var sql = MatchService.CandidateQuery(db, "user_me", "女", DateTimeOffset.UtcNow.AddDays(-30))
            .ToQueryString();

        Assert.Equal(3, Count(sql, "NOT EXISTS"));
        Assert.Contains("user_settings", sql);
        Assert.Contains("matches", sql);
        Assert.Contains("user_blocks", sql);
        Assert.Contains("gender", sql);
        Assert.DoesNotContain("photo_data_urls", sql);
        Assert.DoesNotContain("bio", sql);
        Assert.DoesNotContain("nickname", sql);
    }

    /// <summary>沒指定性別時不該多一個性別條件。</summary>
    [Fact]
    public void TheCandidatePoolHasNoGenderFilterWhenNoneWasStated()
    {
        using var db = Postgres();

        var sql = MatchService.CandidateQuery(db, "user_me", null, DateTimeOffset.UtcNow.AddDays(-30))
            .ToQueryString();

        Assert.DoesNotContain("gender\" =", sql);
        Assert.Equal(3, Count(sql, "NOT EXISTS"));
    }

    /// <summary>每個人最新的一筆今日：挑哪一筆要由資料庫做（GROUP BY ＋ max），不是整張表讀回來。</summary>
    [Fact]
    public void TheLatestEntryPerPersonIsPickedByTheDatabase()
    {
        using var db = Postgres();

        var sql = MatchService.LatestEntriesQuery(db).ToQueryString();

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("max(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JOIN", sql);
        Assert.Contains("PRIVATE", sql);
    }

    /// <summary>
    /// 樹洞列表的摘要模式：回應的內文、圖片、語音都不該出現在這句 SQL 裡——
    /// 省掉的就是它們。完整模式則要照舊全部拿。
    /// </summary>
    [Fact]
    public void TheWallSummaryDoesNotFetchReplyContent()
    {
        using var db = Postgres();

        var summary = RantService.WallRepliesQuery(db, ["rant_a", "rant_b"], summaryOnly: true).ToQueryString();
        var full = RantService.WallRepliesQuery(db, ["rant_a", "rant_b"], summaryOnly: false).ToQueryString();

        Assert.DoesNotContain("content", summary);
        Assert.DoesNotContain("image_data_url", summary);
        Assert.DoesNotContain("audio_data_url", summary);
        Assert.Contains("parent_reply_id", summary);
        Assert.Contains("hidden_at", summary);
        Assert.Contains("deleted_at", summary);
        Assert.Contains("content", full);
    }

    /// <summary>
    /// 互動端點開頭的守門：貼文與作者的帳號狀態要是同一句（LEFT JOIN），
    /// 不是先讀貼文、再拿作者去問第二趟。
    /// </summary>
    [Fact]
    public void ThePostGateIsOneQuery()
    {
        using var db = Postgres();

        var sql = RantService.PostAccessQuery(db, "rant_a").ToQueryString();

        Assert.Contains("LEFT JOIN", sql);
        Assert.Contains("rant_posts", sql);
        Assert.Contains("users", sql);
        Assert.Contains("suspended_until", sql);
        Assert.DoesNotContain("content", sql);
    }

    private static int Count(string text, string needle)
    {
        var count = 0;
        for (var at = text.IndexOf(needle, StringComparison.Ordinal);
             at >= 0;
             at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }
}
