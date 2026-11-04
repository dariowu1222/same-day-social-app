using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Application.Services;

/// <summary>
/// 後台的 AI 用量與花費查詢。只讀 ai_usage_logs，不碰 OpenAI。
///
/// 為什麼不去打 OpenAI 的用量 API：那需要 Admin API key（跟一般 API key 是兩回事），
/// 而且它以「天 × 模型」彙總，回不出「哪一則今日花了多少」。要對到單筆就只能自己記。
/// 代價是這裡顯示的是我們自己乘出來的估算值，與對方帳單可能有分錢級的差異。
/// </summary>
public sealed class AiUsageService
{
    private readonly AppDbContext? db;
    private readonly AiAnalyzerOptions options;

    public AiUsageService(IServiceProvider services, IOptions<AiAnalyzerOptions> options)
    {
        db = services.GetService<AppDbContext>();
        this.options = options.Value;
    }

    /// <summary>一段期間的彙總。金額是估算值，單位美元。</summary>
    public sealed record Bucket(int Calls, int FailedCalls, long InputTokens, long OutputTokens,
        long ReasoningTokens, decimal CostUsd);

    public sealed record Summary(
        bool Configured,
        /// <summary>ai_usage_logs 這張表在不在。這個專案不跑 EF migration，
        /// SQL 要人工貼到 Supabase 執行，所以「忘了建表」是會真的發生的狀況。</summary>
        bool StorageReady,
        string Model,
        decimal InputPricePerMTok,
        decimal CachedPricePerMTok,
        decimal OutputPricePerMTok,
        Bucket Today,
        Bucket Last7Days,
        Bucket ThisMonth,
        Bucket AllTime,
        int AverageLatencyMs,
        DateTimeOffset? LastCallAt);

    public sealed record UsageEntry(
        string Id,
        string? TodayEntryId,
        string Model,
        string Status,
        int InputTokens,
        int CachedInputTokens,
        int OutputTokens,
        int ReasoningTokens,
        decimal CostUsd,
        int LatencyMs,
        string? Error,
        DateTimeOffset CreatedAt);

    private static readonly Bucket Empty = new(0, 0, 0, 0, 0, 0m);

    /// <summary>
    /// 摘要。四個期間一次算完。
    ///
    /// 時間基準用 UTC：created_at 是 timestamptz、C# 端一律 DateTimeOffset.UtcNow，
    /// 這裡若改用本地時間，「今日」的界線會跟寫入時對不上。後台顯示時才轉台北時間。
    /// </summary>
    public Summary GetSummary()
    {
        var model = options.Model;
        var configured = !string.IsNullOrWhiteSpace(options.ApiKey);

        Summary Blank(bool storageReady) => new(configured, storageReady, model,
            options.InputPricePerMTok, options.CachedInputPricePerMTok, options.OutputPricePerMTok,
            Empty, Empty, Empty, Empty, 0, null);

        if (db == null) return Blank(false);

        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var weekStart = todayStart.AddDays(-6);
        var monthStart = new DateTimeOffset(new DateTime(now.UtcDateTime.Year, now.UtcDateTime.Month, 1), TimeSpan.Zero);

        try
        {
            var all = db.AiUsageLogs.AsNoTracking();

            return new Summary(configured, true, model,
                options.InputPricePerMTok, options.CachedInputPricePerMTok, options.OutputPricePerMTok,
                Aggregate(all, todayStart),
                Aggregate(all, weekStart),
                Aggregate(all, monthStart),
                Aggregate(all, null),
                AverageLatency(all),
                all.OrderByDescending(x => x.CreatedAt).Select(x => (DateTimeOffset?)x.CreatedAt).FirstOrDefault());
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // 42P01：SQL 還沒貼到 Supabase 跑。回 StorageReady = false 讓後台直接說
            // 「要先建表」，比丟 500 讓人對著一個沒頭沒尾的錯誤猜要有用得多。
            return Blank(false);
        }
    }

    private static Bucket Aggregate(IQueryable<Infrastructure.Persistence.Models.AiUsageLogRecord> q, DateTimeOffset? from)
    {
        if (from.HasValue) q = q.Where(x => x.CreatedAt >= from.Value);

        // 一次 GroupBy 換一趟往返，而不是六個 Sum 打六次。
        // Sum 在空集合上回 null，所以型別都要是 nullable 再 ?? 0。
        var row = q.GroupBy(_ => 1).Select(g => new
        {
            Calls = g.Count(),
            Failed = g.Count(x => x.Status != "OK"),
            Input = (long?)g.Sum(x => (long)x.InputTokens),
            Output = (long?)g.Sum(x => (long)x.OutputTokens),
            Reasoning = (long?)g.Sum(x => (long)x.ReasoningTokens),
            Cost = (decimal?)g.Sum(x => x.EstimatedCostUsd),
        }).FirstOrDefault();

        return row == null
            ? Empty
            : new Bucket(row.Calls, row.Failed, row.Input ?? 0, row.Output ?? 0, row.Reasoning ?? 0, row.Cost ?? 0m);
    }

    private static int AverageLatency(IQueryable<Infrastructure.Persistence.Models.AiUsageLogRecord> q)
    {
        // 逾時與未送出的那幾筆 latency 無意義（0 或整個 timeout），只看真的有回應的
        var rows = q.Where(x => x.LatencyMs > 0 && x.Status != "TIMEOUT");
        return rows.Any() ? (int)rows.Average(x => x.LatencyMs) : 0;
    }

    /// <summary>明細，新的在前。</summary>
    public (List<UsageEntry> Items, int Total) Search(string? status, int page, int pageSize)
    {
        if (db == null) return ([], 0);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.AiUsageLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            query = s == "FAILED" ? query.Where(x => x.Status != "OK") : query.Where(x => x.Status == s);
        }

        try
        {
            var total = query.Count();
            var items = query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new UsageEntry(
                    x.Id, x.TodayEntryId, x.Model, x.Status,
                    x.InputTokens, x.CachedInputTokens, x.OutputTokens, x.ReasoningTokens,
                    x.EstimatedCostUsd, x.LatencyMs, x.Error, x.CreatedAt))
                .ToList();

            return (items, total);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // 表還沒建。摘要那邊會把 StorageReady 回 false，由後台統一講這件事，
            // 這裡安靜回空集合就好——回 404 會把管理員踢回登入頁。
            return ([], 0);
        }
    }
}
