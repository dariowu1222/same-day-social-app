using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Application.Services;

/// <summary>
/// 「今日」的 AI 分析排隊處理。
///
/// 為什麼要非同步：今日的 POST 在使用者的關鍵路徑上——送出之後滑卡頁會
/// await 這支請求才去取卡（見 TodayPage.submitToday 與 MatchResultPage）。
/// 在那條路徑上插一個外部 API 呼叫，等待時間會直接加上去，而 Render 免費
/// 方案冷啟動本來就已經很慢。
///
/// 所以流程是：關鍵字分析先寫進去、立刻回 200，AI 分析排進佇列背景跑完
/// 再覆蓋那一列。代價是「送出後馬上看到的第一副牌」用的還是關鍵字標籤，
/// 下一次取卡才會吃到 AI 的結果。
///
/// 佇列刻意有界（Capacity）且滿了就丟掉最舊的：這是品質提升不是正確性需求，
/// 塞爆記憶體比少分析幾則嚴重得多。
/// </summary>
public sealed class TodayAiEnrichmentQueue
{
    public sealed record Job(string EntryId, string Content, ResponseMode ResponseMode);

    private readonly Channel<Job> channel = Channel.CreateBounded<Job>(
        new BoundedChannelOptions(200)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public ChannelReader<Job> Reader => channel.Reader;

    /// <summary>排不進去就算了，不要讓呼叫端等。</summary>
    public void Enqueue(Job job) => channel.Writer.TryWrite(job);
}

public sealed class TodayAiEnrichmentWorker(
    TodayAiEnrichmentQueue queue,
    AiTodayAnalyzer analyzer,
    IServiceScopeFactory scopes,
    ILogger<TodayAiEnrichmentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!analyzer.IsConfigured)
        {
            logger.LogInformation("今日 AI 分析未設定，背景處理不啟動");
            return;
        }

        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // 單一則失敗不能讓整個 worker 收掉，否則之後所有今日都不會再被分析。
                logger.LogWarning(ex, "今日 AI 分析處理失敗 entryId={EntryId}", job.EntryId);
            }
        }
    }

    private async Task ProcessAsync(TodayAiEnrichmentQueue.Job job, CancellationToken ct)
    {
        var outcome = await analyzer.AnalyzeAsync(job.Content, job.ResponseMode, ct);

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetService<AppDbContext>();
        if (db == null) return;         // JSON 檔案模式不做這段

        // 不管成不成功都先記帳。失敗的那幾次 token 一樣燒掉了，而且正是最需要
        // 在後台被看見的——只記成功的會讓帳面系統性偏低。
        await WriteUsageAsync(db, job.EntryId, outcome, ct);

        if (outcome.Analysis is not { } analysis) return;   // 失敗原因已記在 log 與 ai_usage_logs

        var record = await db.TodayEntries.FirstOrDefaultAsync(x => x.Id == job.EntryId, ct);
        if (record == null)
        {
            // 使用者在這短短幾秒內把帳號砍了之類的。沒什麼好做的。
            logger.LogInformation("今日記錄已不存在，略過 AI 分析 entryId={EntryId}", job.EntryId);
            return;
        }

        // 只覆寫 event 與 emotions。ValueTags / InterestTags 刻意不動，保留
        // TodayAnalyzerService 當初寫進去的值。
        //
        // 依據是實測：五輪 A/B（每臂 20 次呼叫）裡，AI 剩下的 6 個幻覺標籤全部
        // 落在 interests（「清冰箱」→ FOOD 這種名詞聯想），而 event 與 emotions
        // 乾淨——否定句、假「氣」、純敘事給空陣列 5/5 全對，正好是關鍵字分析器
        // 最弱的地方。拿走增益，不引進它唯一的新風險。
        //
        // AiTodayAnalyzer 現在連問都不問那兩欄，所以 analysis.ValueTags 恆為空；
        // 就算哪天又問了，這裡也不該照抄——要改成寫入前先確認它真的比較準。
        record.EventType = analysis.EventType.ToString();
        record.EmotionTags = analysis.EmotionTags.Select(x => x.ToString()).ToArray();
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "今日 AI 分析完成 entryId={EntryId} event={Event} emotions={Emotions}（interests/values 維持關鍵字結果）",
            job.EntryId, analysis.EventType, string.Join(",", analysis.EmotionTags));
    }

    /// <summary>
    /// 把這次的用量寫進 ai_usage_logs。
    ///
    /// 整段包 try/catch 並吞掉例外是刻意的：記帳壞掉不該把原本容錯的分析路徑
    /// 變成會炸的。最常見的情況是 ai_usage_logs 這張表還沒在 Supabase 建起來
    /// （這個專案不跑 EF migration，SQL 要人工執行），那會丟 42P01；
    /// 那種時候今日分析本身還是該照常完成。
    /// </summary>
    private async Task WriteUsageAsync(
        AppDbContext db, string entryId, AiTodayAnalyzer.AiCallOutcome o, CancellationToken ct)
    {
        try
        {
            db.AiUsageLogs.Add(new AiUsageLogRecord
            {
                Id = $"aiu_{Guid.NewGuid():N}",
                TodayEntryId = entryId,
                Model = o.Model,
                Status = o.Status,
                InputTokens = o.InputTokens,
                CachedInputTokens = o.CachedTokens,
                OutputTokens = o.OutputTokens,
                ReasoningTokens = o.ReasoningTokens,
                EstimatedCostUsd = o.CostUsd,
                InputPricePerMTok = o.InputPricePerMTok,
                CachedPricePerMTok = o.CachedPricePerMTok,
                OutputPricePerMTok = o.OutputPricePerMTok,
                LatencyMs = o.LatencyMs,
                // varchar(500)：模型拒絕的理由或例外訊息都可能很長，截斷比寫不進去好
                Error = o.Error is { Length: > 500 } e ? e[..500] : o.Error,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI 用量記錄寫入失敗 entryId={EntryId}（不影響今日分析）", entryId);

            // 寫失敗的那個 entity 還掛在 change tracker 上，不清掉的話
            // 下面更新 today_entries 的 SaveChanges 會把它再送一次、再失敗一次。
            db.ChangeTracker.Clear();
        }
    }
}
