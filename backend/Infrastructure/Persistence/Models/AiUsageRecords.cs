namespace SameDaySocialApp.Infrastructure.Persistence.Models;

/// <summary>
/// 一次 AI 分析呼叫的用量與花費。成功與失敗都存——被截斷、被模型拒絕的那幾次
/// token 已經燒掉也已經計費，只存成功的會讓帳面系統性偏低。
///
/// 為什麼不只靠 log：OpenAI 的用量頁以「天 × 模型」彙總，單筆查不到，而且
/// 單筆金額小到會被四捨五入成 $0.00；應用層 log 又會隨 Render 休眠消失。
///
/// 為什麼把三個單價也存進來：成本是我們自己乘出來的。單價會隨換模型、隨對方
/// 調價而變，只存結果的話舊記錄就再也驗算不回來了。
/// </summary>
public sealed class AiUsageLogRecord
{
    public string Id { get; set; } = "";

    /// <summary>對應的今日記錄。刻意不設外鍵：帳務記錄要活得比它指向的東西久。</summary>
    public string? TodayEntryId { get; set; }

    public string Model { get; set; } = "";

    /// <summary>AiTodayAnalyzer.CallStatus 的其中一個值（OK / TRUNCATED / ...）。</summary>
    public string Status { get; set; } = "";

    public int InputTokens { get; set; }

    /// <summary>命中快取的輸入 token。單價只有一般輸入的十分之一，要分開算。</summary>
    public int CachedInputTokens { get; set; }

    public int OutputTokens { get; set; }

    /// <summary>
    /// 推理 token。已經含在 OutputTokens 裡，單獨留一欄是因為它算輸出價、
    /// 又是這顆模型最容易暴衝的一項；混在總數裡就看不出成本是被哪一項推上去的。
    /// </summary>
    public int ReasoningTokens { get; set; }

    public decimal EstimatedCostUsd { get; set; }

    public decimal InputPricePerMTok { get; set; }
    public decimal CachedPricePerMTok { get; set; }
    public decimal OutputPricePerMTok { get; set; }

    public int LatencyMs { get; set; }

    /// <summary>失敗時的原因。成功為 null。</summary>
    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
