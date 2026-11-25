using Npgsql;

namespace SameDaySocialApp.Infrastructure.Persistence;

/// <summary>
/// 連線字串沒有寫的連線池設定，在這裡補上預設值。
///
/// 為什麼需要（2026-11-25 實測）：線上與本機的連線字串都帶著
/// <c>Maximum Pool Size=8;Connection Idle Lifetime=20</c>——那是 6 月為了不要把
/// Supabase session pooler 的連線槽吃滿而加的。它解決了那個問題，但副作用是
/// 「最少留幾條」沒有設（預設 0），於是只要 20 秒沒有人用，連線就一條不剩。
/// 重新建一條要 0.3–0.4 秒（TLS 加上 pooler 的登入），而使用者打開 App、
/// 切到另一個分頁、停下來讀一篇文章再按愛心，中間幾乎都超過 20 秒——
/// 等於大部分的操作都先付這一筆。同時發出的請求更糟：每一個都要各建一條。
///
/// 做法：留三條熱的（一個畫面通常同時發兩三個請求）。多出來的照舊 20 秒就放掉，所以槽位的問題不會回來
/// （一個後端平常只佔 3 條，尖峰最多 8 條，跟原本一樣）。
///
/// 只在連線字串「沒有寫」的時候才補。要調整的人直接在連線字串裡寫
/// <c>Minimum Pool Size=…</c> 或 <c>Keepalive=…</c> 就會以它為準，不必改程式。
/// </summary>
public static class ConnectionPoolDefaults
{
    /// <summary>沒有流量時留著不關的連線數。</summary>
    public const int WarmConnections = 3;

    /// <summary>
    /// 連線閒著超過這麼久就送一個空查詢。留著的那兩條如果真的完全不動，
    /// 中間的 pooler 或網路設備會默默把它斷掉，下一個請求拿到的是一條死的連線。
    /// </summary>
    public const int KeepAliveSeconds = 30;

    public static string Apply(string connectionString)
    {
        var written = WrittenKeywords(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (!written.Contains("minimumpoolsize") && !written.Contains("minpoolsize"))
        {
            // 不能比上限大，否則 Npgsql 開不了連線池
            builder.MinPoolSize = Math.Min(WarmConnections, builder.MaxPoolSize);
        }

        if (!written.Contains("keepalive"))
        {
            builder.KeepAlive = KeepAliveSeconds;
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// 連線字串裡「真的有寫」的關鍵字，去掉空白、轉小寫。
    ///
    /// 不能問 NpgsqlConnectionStringBuilder.ContainsKey：它對每一個它認得的關鍵字
    /// 都回 true，不管連線字串裡有沒有寫（實測過——第一版就是這樣寫的，結果預設值
    /// 一個都沒補上去）。所以用不認識任何關鍵字的那個基底類別來拆。
    /// </summary>
    private static HashSet<string> WrittenKeywords(string connectionString)
    {
        var plain = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
        return plain.Keys.Cast<string>()
            .Select(key => key.Replace(" ", "").ToLowerInvariant())
            .ToHashSet();
    }
}
