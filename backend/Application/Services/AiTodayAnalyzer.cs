using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Application.Services;

public sealed class AiAnalyzerOptions
{
    /// <summary>沒設就整條停用，退回關鍵字分析器——跟推播同一個原則。</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 用 OpenAI 最便宜的那一顆。這是抽取／分類，不需要推理深度，
    /// 模型等級拉高對標籤品質幾乎沒有幫助，只是把每則的成本乘上去。
    /// gpt-5-nano 是目前官方價目表上輸入與輸出都最低的（$0.05 / $0.40 每百萬 token）。
    /// </summary>
    public string Model { get; set; } = "gpt-5-nano";

    /// <summary>單次呼叫的逾時。逾時就放棄，保留關鍵字分析的結果。</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// 覆寫 system prompt。正式環境不要設，留空就用 DefaultSystemPrompt。
    ///
    /// 存在的理由是 prompt 要能被評測：比較兩版 prompt 時，兩邊必須走同一條
    /// 程式路徑，只差這個字串，否則比到的是兩套不同的實作而不是兩套 prompt。
    /// </summary>
    public string? SystemPromptOverride { get; set; }

    // 每百萬 token 的單價（美元），只用來把 log 裡的成本換算出來，不影響任何行為。
    // 預設是 gpt-5-nano 的牌價；換模型或對方調價時記得一起改，不然 log 會說謊。
    // 真正的帳單以 https://platform.openai.com/usage 為準。
    public decimal InputPricePerMTok { get; set; } = 0.05m;
    public decimal CachedInputPricePerMTok { get; set; } = 0.005m;
    public decimal OutputPricePerMTok { get; set; } = 0.40m;
}

/// <summary>
/// 用 OpenAI 取代關鍵字比對來分析「今日」。
///
/// 為什麼需要：TodayAnalyzerService 是子字串比對，不處理否定（「今天不累」
/// 會配到「累」），而且有單字關鍵字（「氣」會中天氣、客氣、運氣）。
/// 配對分數整個建立在這些標籤上，錯的標籤會讓人被配給情緒相反的人。
///
/// 為什麼不直接取代：這支會打外部 API，會慢、會失敗。今日是使用者的主要
/// 動線，不能因為對方抖一下就寫不進去。所以關鍵字分析器留著當底，
/// 這支在背景跑完再覆蓋（見 TodayAiEnrichmentQueue）。
/// </summary>
public sealed class AiTodayAnalyzer
{
    private readonly AiAnalyzerOptions options;
    private readonly ILogger<AiTodayAnalyzer> logger;
    private readonly Lazy<ChatClient?> client;

    public AiTodayAnalyzer(IOptions<AiAnalyzerOptions> options, ILogger<AiTodayAnalyzer> logger)
    {
        this.options = options.Value;
        this.logger = logger;
        client = new Lazy<ChatClient?>(CreateClient, isThreadSafe: true);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ApiKey);

    private string SystemPrompt => string.IsNullOrWhiteSpace(options.SystemPromptOverride)
        ? DefaultSystemPrompt
        : options.SystemPromptOverride;

    private ChatClient? CreateClient()
    {
        if (!IsConfigured)
        {
            logger.LogInformation("未設定 Ai:ApiKey，今日的 AI 分析停用，改用關鍵字分析器");
            return null;
        }

        return new ChatClient(options.Model, options.ApiKey);
    }

    /// <summary>
    /// 第一版把「寧缺勿濫」放在最上面的規則區，離選項清單很遠，結果實測下來
    /// 模型把每個欄位的 maxItems 當成配額在填：純敘事的句子照樣給 CALM+HAPPY，
    /// 提到「冰箱」就標 FOOD。這版把克制的指示搬到每個欄位的選項旁邊，
    /// 並補兩個全空陣列的範例——範例刻意不用任何評測句，否則是對著答案出題。
    /// </summary>
    public const string DefaultSystemPrompt = """
        你是「同頻 Today」的今日分析器。使用者會寫一段當天的心情，你要抽出標籤供配對使用。

        這些標籤會拿去把兩個真實的人配在一起。多標一個，不相干的人會被湊成一對，
        而且事後查不出是哪個標籤害的；少標一個，只是少一次配對機會。
        所以沒有明確寫出來的一律不標，空陣列是很常見的正確答案。

        只看使用者真的寫出來的字，不要推測：
        - 注意否定。「今天不累」「我沒有生氣」不可以標成 TIRED 或 ANGRY。
        - 不要從名詞聯想。提到某個物品、地點或場合，不代表使用者在講那件事或那個興趣。
        - 不要從語氣推測未明說的情緒。

        event（必選一個）
        WORK_STRESS 工作上受挫、有壓力 / WORK 工作，但不帶壓力（順利、普通、甚至愉快）
        FAMILY 家庭 / FRIENDSHIP 朋友 / RELATIONSHIP 感情
        STUDY 學業 / MONEY 金錢 / HEALTH 健康 / SMALL_HAPPINESS 小確幸 / RANDOM 看不出來
        → 看不出來就給 RANDOM，不要硬挑一個最接近的。
        → 講到工作時先分清楚是哪一種：有受挫、抱怨、壓力才算 WORK_STRESS，
          單純在講今天做了什麼工作、或工作順利，是 WORK。

        emotions（預設空陣列，最多 3 個）
        TIRED 累 / ANGRY 生氣 / SAD 難過 / LONELY 孤單 / ANXIOUS 焦慮
        HAPPY 開心 / EXCITED 期待 / WRONGED 委屈 / CALM 平靜
        → 只在使用者明確表達情緒時才給。純粹在敘述做了什麼事，就是空陣列。
        → CALM 只在使用者真的說自己平靜時才給，不可以拿來填空位。

        「最多 3 個」是上限，不是配額。寧可留空。

        兩個例子：

        輸入：今天去剪頭髮，順便把水電費繳了。
        輸出：{"event":"RANDOM","emotions":[]}

        輸入：跟男朋友吵架，氣到睡不著。
        輸出：{"event":"RELATIONSHIP","emotions":["ANGRY"]}
        """;

    private const int MaxEmotions = 3;

    /// <summary>
    /// 只問 event 和 emotions，不問 values / interests。
    ///
    /// 不是懶，是實測結果：五輪 A/B（每臂 20 次呼叫）裡，新 prompt 把幻覺標籤從
    /// 17 降到 6，而那剩下的 6 個**全部**出現在 interests——「清冰箱」被標成 FOOD
    /// 之類的名詞聯想。event 與 emotions 則乾淨（否定句、假「氣」、純敘事給空陣列
    /// 5/5 全對），正是關鍵字分析器最弱的地方。
    ///
    /// 所以分工：AI 只覆寫它確定比較強的兩欄，interests / values 保留
    /// TodayAnalyzerService 的結果。拿走增益，不引進它唯一的新風險。
    /// 連問都不問還省了 token——欄位不存在，就沒有東西可以幻覺。
    ///
    /// strict 模式有兩個硬性要求：每個 property 都要列進 required，
    /// 而且 additionalProperties 一定要 false。少一個就直接 400。
    /// </summary>
    private static BinaryData BuildSchema()
    {
        var schema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "event", "emotions" },
            properties = new
            {
                @event = new { type = "string", @enum = Enum.GetNames<EventType>() },
                emotions = new
                {
                    type = "array",
                    maxItems = MaxEmotions,
                    items = new { type = "string", @enum = Enum.GetNames<EmotionTag>() },
                },
            },
        };

        return BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(schema));
    }

    private static readonly BinaryData Schema = BuildSchema();

    // OPENAI001：ReasoningEffortLevel 在 SDK 裡還標著 Experimental。範圍壓到這一個
    // 方法，不要整個專案關掉——SDK 升版把它改名或移掉時，要在這裡編不過，
    // 而不是在別的地方悄悄換行為。真的沒了就拿掉這段，退回預設 effort，
    // 那只會變貴變慢，不會壞。
#pragma warning disable OPENAI001
    private ChatCompletionOptions BuildRequestOptions() => new()
    {
        // 這是分類任務，不需要思考。gpt-5 系列的 reasoning token 是算輸出價的，
        // 預設 medium 會把每則成本墊高一個量級，所以壓到最低。
        ReasoningEffortLevel = ChatReasoningEffortLevel.Minimal,

        // 對推理模型來說這個上限含 reasoning token。設太小會被截斷成半截 JSON，
        // 反而解析失敗；設大不會多花錢（只付實際用掉的），所以寬鬆給。
        MaxOutputTokenCount = 2000,

        ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "today_analysis",
            jsonSchema: Schema,
            jsonSchemaIsStrict: true),
    };
#pragma warning restore OPENAI001

    /// <summary>
    /// 一次呼叫的完整結果：分析出來的標籤（失敗時為 null）加上這次的用量與花費。
    ///
    /// 為什麼不只回 TodayAnalysis：失敗的那幾次一樣要付錢，而且正是最需要被看見的
    /// （被截斷、被拒絕、逾時）。只回標籤的話，這些錢就從帳上消失了。
    /// Status 的值會原封不動寫進 ai_usage_logs.status，改字串等於改資料。
    /// </summary>
    public sealed record AiCallOutcome(
        TodayAnalysis? Analysis,
        string Status,
        string Model,
        int InputTokens,
        int CachedTokens,
        int OutputTokens,
        int ReasoningTokens,
        decimal CostUsd,
        decimal InputPricePerMTok,
        decimal CachedPricePerMTok,
        decimal OutputPricePerMTok,
        int LatencyMs,
        string? Error)
    {
        public bool Succeeded => Analysis != null;
    }

    public static class CallStatus
    {
        public const string Ok = "OK";
        public const string Truncated = "TRUNCATED";
        public const string Refused = "REFUSED";
        public const string EmptyContent = "EMPTY_CONTENT";
        public const string ParseFailed = "PARSE_FAILED";
        public const string Timeout = "TIMEOUT";
        public const string Error = "ERROR";
    }

    /// <summary>
    /// 分析一段今日內容。Analysis 為 null 代表這次沒分析成功（逾時、API 失敗、
    /// 回應解析不出來），呼叫端要保留原本關鍵字分析的結果；但用量仍然要記帳。
    ///
    /// 計時與記 log 都收在這一層：裡面有七個 return，每個都自己蓋時間、自己印一行
    /// 的話，遲早會漏掉其中一條，而漏掉的那條通常就是出事的那條。
    /// </summary>
    public async Task<AiCallOutcome> AnalyzeAsync(string content, ResponseMode responseMode, CancellationToken ct)
    {
        var started = Stopwatch.StartNew();
        var outcome = await RunAsync(content, responseMode, ct);
        outcome = outcome with { LatencyMs = (int)started.ElapsedMilliseconds };
        LogOutcome(outcome);
        return outcome;
    }

    private async Task<AiCallOutcome> RunAsync(string content, ResponseMode responseMode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(content)) return Skipped(CallStatus.EmptyContent);

        var api = client.Value;
        if (api == null) return Skipped(CallStatus.Error, "未設定 Ai:ApiKey");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        try
        {
            ChatMessage[] messages =
            [
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(content),
            ];

            var completion = await api.CompleteChatAsync(messages, BuildRequestOptions(), timeout.Token);
            var result = completion.Value;

            // 用量先算出來再判斷成不成功：被截斷、被拒絕的那幾次一樣要付錢，
            // 只記成功的會讓帳對不起來。
            var usage = Measure(result.Usage);

            if (!string.IsNullOrEmpty(result.Refusal))
            {
                return usage with { Status = CallStatus.Refused, Error = result.Refusal };
            }

            if (result.FinishReason == ChatFinishReason.Length)
            {
                // 幾乎只會在 reasoning 暴衝時發生。狀態寫清楚是被截斷，
                // 不然只會看到一句「不是合法 JSON」，查起來繞遠路。
                return usage with { Status = CallStatus.Truncated };
            }

            var json = new StringBuilder();
            foreach (var part in result.Content)
            {
                if (part.Kind == ChatMessageContentPartKind.Text) json.Append(part.Text);
            }

            if (json.Length == 0)
            {
                return usage with { Status = CallStatus.ParseFailed, Error = "回應沒有文字內容" };
            }

            var analysis = Parse(json.ToString(), responseMode);
            return analysis == null
                ? usage with { Status = CallStatus.ParseFailed, Error = "回應不是合法 JSON" }
                : usage with { Status = CallStatus.Ok, Analysis = analysis };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 逾時拿不到 usage。對方可能已經算完了、也可能沒有，我們無從得知，
            // 所以 token 記 0 而不是亂估——帳寧可少記，不要記錯。
            return Skipped(CallStatus.Timeout, $"超過 {options.TimeoutSeconds} 秒");
        }
        catch (Exception ex)
        {
            return Skipped(CallStatus.Error, ex.Message);
        }
    }

    /// <summary>沒有真的打出去（或打出去但拿不到用量）的那幾種收場。單價照樣帶上，
    /// 後台才知道這筆是在哪個價目下發生的。</summary>
    private AiCallOutcome Skipped(string status, string? error = null) =>
        new(null, status, options.Model, 0, 0, 0, 0, 0m,
            options.InputPricePerMTok, options.CachedInputPricePerMTok, options.OutputPricePerMTok,
            0, error);

    /// <summary>
    /// 把 SDK 回的用量換算成我們自己的記帳單位。Status 先擺 OK，由呼叫端覆寫。
    ///
    /// reasoning token 特別要單獨留一欄：它算在輸出價裡，是這顆模型最容易暴衝的
    /// 一項，混在 output 裡就看不出是暴衝還是真的講很多。
    /// </summary>
    private AiCallOutcome Measure(ChatTokenUsage? usage)
    {
        if (usage == null) return Skipped(CallStatus.Ok);

        var cached = usage.InputTokenDetails?.CachedTokenCount ?? 0;
        var fresh = usage.InputTokenCount - cached;
        var reasoning = usage.OutputTokenDetails?.ReasoningTokenCount ?? 0;

        var cost = (fresh * options.InputPricePerMTok
                    + cached * options.CachedInputPricePerMTok
                    + usage.OutputTokenCount * options.OutputPricePerMTok) / 1_000_000m;

        return new AiCallOutcome(
            null, CallStatus.Ok, options.Model,
            usage.InputTokenCount, cached, usage.OutputTokenCount, reasoning, cost,
            options.InputPricePerMTok, options.CachedInputPricePerMTok, options.OutputPricePerMTok,
            0, null);
    }

    /// <summary>
    /// 每次呼叫寫一行 log。後台有表可以看了，但 log 仍然是出事當下最快的那條線索，
    /// 而且資料庫寫入自己也可能失敗，兩邊互為備援。
    /// 失敗的收場用 Warning，掃 log 時一眼就分得出來。
    /// </summary>
    private void LogOutcome(AiCallOutcome o)
    {
        const string template =
            "今日 AI 用量 status={Status} model={Model} input={Input}（cached={Cached}） "
            + "output={Output}（reasoning={Reasoning}） {Latency}ms 約 US${Cost:F6}／千則 US${PerThousand:F3} {Error}";

        if (o.Succeeded)
        {
            logger.LogInformation(template, o.Status, o.Model, o.InputTokens, o.CachedTokens,
                o.OutputTokens, o.ReasoningTokens, o.LatencyMs, o.CostUsd, o.CostUsd * 1000m, "");
        }
        else
        {
            logger.LogWarning(template, o.Status, o.Model, o.InputTokens, o.CachedTokens,
                o.OutputTokens, o.ReasoningTokens, o.LatencyMs, o.CostUsd, o.CostUsd * 1000m, o.Error ?? "");
        }
    }

    /// <summary>
    /// 把模型回的 JSON 轉成 TodayAnalysis。
    /// 即使用了 strict schema，認不得的值一律丟掉而不是硬塞——模型回一個不存在的
    /// enum 名稱時，寧可少一個標籤，也不要讓它變成例外往上丟。
    /// 數量上限這裡再砍一次：schema 的 maxItems 是請求對方遵守，不是我方的保證。
    /// </summary>
    private TodayAnalysis? Parse(string json, ResponseMode responseMode)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var eventType = EventType.RANDOM;
            if (root.TryGetProperty("event", out var ev)
                && Enum.TryParse<EventType>(ev.GetString(), out var parsedEvent))
            {
                eventType = parsedEvent;
            }

            var emotions = ReadStrings(root, "emotions")
                .Select(x => Enum.TryParse<EmotionTag>(x, out var tag) ? tag : (EmotionTag?)null)
                .Where(x => x != null)
                .Select(x => x!.Value)
                .Distinct()
                .Take(MaxEmotions)
                .ToList();

            // ValueTags / InterestTags 一律空：這支不負責那兩欄（見 BuildSchema 的說明）。
            // 呼叫端必須保留關鍵字分析器的結果，不可以拿這裡的空集合去覆寫，
            // 否則使用者的興趣標籤會被這支清空。TodayAiEnrichmentWorker 已照此處理。
            return new TodayAnalysis(eventType, emotions, [], [], responseMode);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "今日 AI 分析回應不是合法 JSON，保留關鍵字結果");
            return null;
        }
    }

    private static List<string> ReadStrings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return arr.EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();
    }
}
