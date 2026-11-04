using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Application.Services;

public sealed class TodayEntryService
{
    private readonly JsonStorageService storage;
    private readonly TodayAnalyzerService analyzer;
    private readonly AppDbContext? db;
    private readonly TodayAiEnrichmentQueue? aiQueue;
    private readonly AiTodayAnalyzer? aiAnalyzer;
    private readonly ILogger<TodayEntryService> logger;

    public TodayEntryService(
        JsonStorageService storage,
        TodayAnalyzerService analyzer,
        ILogger<TodayEntryService> logger,
        IServiceProvider services)
    {
        this.storage = storage;
        this.analyzer = analyzer;
        this.logger = logger;
        db = services.GetService<AppDbContext>();
        aiQueue = services.GetService<TodayAiEnrichmentQueue>();
        aiAnalyzer = services.GetService<AiTodayAnalyzer>();
    }

    /// <summary>
    /// 建立一則今日。
    ///
    /// 關鍵字分析先跑、先落地、先回應——這條路徑在使用者的關鍵動線上
    /// （送出後滑卡頁會等這支請求才取卡），不能被外部 API 拖住。
    ///
    /// useAi 為 true 且 AI 分析有設定時，再把這一則排進背景佇列重新分析，
    /// 跑完覆蓋標籤。代價是「送出後的第一副牌」用的還是關鍵字結果。
    /// </summary>
    public (TodayEntry Entry, TodayAnalysis Analysis) Create(
        string userId, string content, ResponseMode responseMode, Visibility visibility, bool useAi = false)
    {
        var analysis = analyzer.Analyze(content, responseMode);
        var entry = new TodayEntry
        {
            Id = $"today_{Guid.NewGuid():N}",
            UserId = userId,
            Content = content.Trim(),
            EventType = analysis.EventType,
            EmotionTags = analysis.EmotionTags,
            ValueTags = analysis.ValueTags,
            InterestTags = analysis.InterestTags,
            ResponseMode = analysis.ResponseMode,
            Visibility = visibility
        };

        if (db != null)
        {
            db.TodayEntries.Add(entry.ToRecord());
            db.SaveChanges();
            QueueAiAnalysis(entry, content, responseMode, useAi);
            return (entry, analysis);
        }

        storage.InsertOne("todayEntries", entry);
        return (entry, analysis);
    }

    /// <summary>
    /// 排進 AI 重新分析。三個條件任一不成立就算了，呼叫端不需要知道——
    /// 關鍵字的結果已經寫進去，使用者的今日不會因此少掉。
    ///
    /// 但每一條都要留下 log。原本三個 return 都是靜默的，結果是：勾了開關、
    /// 送出、什麼都沒發生，從伺服器這邊完全看不出是卡在哪一關，
    /// 最後得跑去 OpenAI 的後台看金鑰的「最後使用」時間才知道呼叫沒送出去。
    /// 這幾行就是為了不要再發生那件事。
    /// </summary>
    private void QueueAiAnalysis(TodayEntry entry, string content, ResponseMode responseMode, bool useAi)
    {
        if (!useAi)
        {
            logger.LogInformation("今日 AI 分析略過：前端沒有勾「會員測試AI」 entryId={EntryId}", entry.Id);
            return;
        }

        if (aiQueue == null || aiAnalyzer is not { IsConfigured: true })
        {
            // 設定是啟動時讀一次的，所以這通常代表後端起來的時候 Ai:ApiKey 還沒設，
            // 不是現在才不見。設好之後要重啟後端才算數。
            logger.LogWarning(
                "今日 AI 分析略過：啟動時沒讀到 Ai:ApiKey（設好要重啟後端） entryId={EntryId}", entry.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            logger.LogInformation(
                "今日 AI 分析略過：只選了心情卡、沒有文字內容 entryId={EntryId}", entry.Id);
            return;
        }

        aiQueue.Enqueue(new TodayAiEnrichmentQueue.Job(entry.Id, content, responseMode));
        logger.LogInformation("今日 AI 分析已排入佇列 entryId={EntryId}", entry.Id);
    }

    /// <summary>
    /// 不依帳號狀態過濾（盤點過，刻意不加）：今日記錄只有本人讀得到——
    /// TodayEntriesController 對 CallerId != userId 一律 Forbid，而本人若不是
    /// ACTIVE，請求在 Program.cs 的中介軟體就被 401 擋下來了。
    ///
    /// 「別人的今天」會外流的地方不在這支，而在配對候選池（MatchService，
    /// 那裡已依帳號狀態過濾）。
    /// </summary>
    public List<TodayEntry> GetByUser(string userId)
    {
        if (db != null)
        {
            return db.TodayEntries
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToList()
                .Select(x => x.ToDomain())
                .ToList();
        }

        return storage.ReadCollection<TodayEntry>("todayEntries")
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }
}
