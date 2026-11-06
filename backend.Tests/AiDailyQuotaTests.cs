using SameDaySocialApp.Application.Services;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 每人每天能用幾次 AI 分析。
///
/// 要不要跑 AI 原本完全由前端那顆「會員測試AI」核取方塊決定，而那是每個使用者
/// 都看得到的。一支腳本可以不停建立今日心情並帶 UseAi=true，直接燒 OpenAI 的
/// 額度——不必打垮任何東西，只要讓帳單變大。分析佇列又是有界的（容量 200，
/// 滿了丟最舊），所以第二個後果是正常使用者的分析被擠掉。
/// </summary>
public sealed class AiDailyQuotaTests
{
    private static readonly DateOnly Today = new(2026, 11, 6);
    private static readonly DateOnly Tomorrow = new(2026, 11, 7);

    private const string Me = "user_me";
    private const string Someone = "user_someone_else";

    [Fact]
    public void TheFirstFewGoThroughAndTheRestDoNot()
    {
        var quota = new AiDailyQuota();

        for (var i = 0; i < AiDailyQuota.PerUserPerDay; i++)
        {
            Assert.True(quota.TryConsume(Me, Today, NoneYet), $"第 {i + 1} 次應該還在額度內");
        }

        Assert.False(quota.TryConsume(Me, Today, NoneYet));
        Assert.False(quota.TryConsume(Me, Today, NoneYet));
    }

    [Fact]
    public void TomorrowStartsOver()
    {
        var quota = new AiDailyQuota();
        for (var i = 0; i < AiDailyQuota.PerUserPerDay; i++)
        {
            quota.TryConsume(Me, Today, NoneYet);
        }

        Assert.True(quota.TryConsume(Me, Tomorrow, NoneYet));
    }

    /// <summary>額度是一人一份。用滿的人不該連累別人。</summary>
    [Fact]
    public void OnePersonBurningTheirQuotaDoesNotBlockAnyoneElse()
    {
        var quota = new AiDailyQuota();
        for (var i = 0; i < AiDailyQuota.PerUserPerDay + 3; i++)
        {
            quota.TryConsume(Me, Today, NoneYet);
        }

        Assert.True(quota.TryConsume(Someone, Today, NoneYet));
    }

    /// <summary>
    /// 計數在記憶體，重啟就空了。所以今天第一次用到的時候要先問資料庫
    /// 「這個人今天已經跑掉幾次」，否則重啟一次等於整份額度重發。
    /// </summary>
    [Fact]
    public void AfterARestartTheCountStartsFromWhatTheDatabaseAlreadyLogged()
    {
        var quota = new AiDailyQuota();

        Assert.False(quota.TryConsume(Me, Today, () => AiDailyQuota.PerUserPerDay));
    }

    [Fact]
    public void TheDatabaseIsOnlyAskedOncePerPersonPerDay()
    {
        var quota = new AiDailyQuota();
        var asked = 0;

        for (var i = 0; i < AiDailyQuota.PerUserPerDay; i++)
        {
            quota.TryConsume(Me, Today, () => { asked++; return 0; });
        }

        Assert.Equal(1, asked);
    }

    /// <summary>
    /// 問不到就從 0 起算（呼叫端查不到用量時會這樣回）。這一條不是守門本身——
    /// 守門是上面那個上限，這裡只是起點。
    /// </summary>
    [Fact]
    public void NotKnowingTheHistoryStillLeavesTheLimitInPlace()
    {
        var quota = new AiDailyQuota();

        for (var i = 0; i < AiDailyQuota.PerUserPerDay; i++)
        {
            Assert.True(quota.TryConsume(Me, Today, () => 0));
        }
        Assert.False(quota.TryConsume(Me, Today, () => 0));
    }

    [Fact]
    public void NobodyInParticularGetsNothing()
    {
        Assert.False(new AiDailyQuota().TryConsume("", Today, NoneYet));
        Assert.False(new AiDailyQuota().TryConsume("   ", Today, NoneYet));
    }

    /// <summary>資料庫裡今天還沒有這個人的用量。</summary>
    private static int NoneYet() => 0;
}
