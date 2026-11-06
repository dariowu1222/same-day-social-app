namespace SameDaySocialApp.Application.Services;

/// <summary>
/// 每人每天能用幾次 AI 分析。
///
/// 為什麼需要：要不要跑 AI 原本完全由前端那顆「會員測試AI」開關決定
/// （request.UseAi），而那是每個使用者都看得到的核取方塊。一支腳本可以
/// 不停建立今日心情並帶 UseAi=true，直接燒 OpenAI 的額度——這是成本型的
/// 阻斷服務，不必打垮任何東西，只要讓帳單變大。
///
/// 分析佇列是有界的（容量 200，滿了丟最舊），所以第二個後果是：正常使用者
/// 的分析會被擠掉，而且他看到的只是「標籤怪怪的」，不會知道為什麼。
///
/// 為什麼計數放在記憶體：ai_usage_logs 是分析真的跑完之後才寫的，要拿它
/// 當閘門會慢一整個佇列的時間——也就是爆量的那一刻正好測不準。這裡改成
/// 排入佇列的當下就記一次，重啟之後再用資料庫裡今天已經記下的次數當起點。
/// 重啟會讓已經用完的人多拿到幾次，代價有限，而且重啟不是攻擊者能挑時機的事。
/// </summary>
public sealed class AiDailyQuota
{
    /// <summary>
    /// 一天五次。這是「測試 AI」的功能，不是主要動線——今日照常送得出去，
    /// 超過上限只是標籤退回原本的關鍵字分析器，使用者的今日不會因此少掉。
    /// </summary>
    public const int PerUserPerDay = 5;

    private readonly object gate = new();
    private readonly Dictionary<string, (DateOnly Day, int Used)> counters = [];

    /// <summary>
    /// 取用一次額度，超過上限回 false。
    /// seedFromHistory 只在這個人今天第一次用到時才會被呼叫（例如剛重啟）。
    /// </summary>
    public bool TryConsume(string userId, DateOnly today, Func<int> seedFromHistory)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        // 讀資料庫不要放在鎖裡面：同一個人同一刻重複進來最多就是多問一次，
        // 而把一次往返關在鎖裡會讓所有人的今日一起排隊。
        var seeded = counters.TryGetValue(userId, out var current) && current.Day == today
            ? (int?)null
            : seedFromHistory();

        lock (gate)
        {
            if (!counters.TryGetValue(userId, out var counter) || counter.Day != today)
            {
                counter = (today, seeded ?? 0);
            }

            if (counter.Used >= PerUserPerDay)
            {
                counters[userId] = counter;
                return false;
            }

            counters[userId] = (today, counter.Used + 1);
            return true;
        }
    }
}
