using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Application.Services;

public sealed record TodayAnalysis(
    EventType EventType,
    List<EmotionTag> EmotionTags,
    List<string> ValueTags,
    List<string> InterestTags,
    ResponseMode ResponseMode);

public sealed class TodayAnalyzerService
{
    public TodayAnalysis Analyze(string content, ResponseMode responseMode)
    {
        var text = content.ToLowerInvariant();
        var eventType = ResolveEventType(text);
        var emotions = ResolveEmotionTags(text);
        var values = ResolveValueTags(text);
        var interests = ResolveInterestTags(text);

        if (emotions.Count == 0)
        {
            emotions.Add(EmotionTag.CALM);
        }

        return new TodayAnalysis(eventType, emotions.Distinct().ToList(), values, interests, responseMode);
    }

    private static EventType ResolveEventType(string text)
    {
        // 工作相關的詞先認出來，再看有沒有受挫的訊號才決定是哪一種。
        //
        // 以前這七個詞全部直接判成 WORK_STRESS，但「上班／同事／會議／客戶」
        // 本身是中性的——「今天上班順手把案子做完了」會被標成工作壓力，
        // 標籤和事實相反，然後拿去跟真的在工作上受挫的人配 +30 分。
        if (ContainsAny(text, "上班", "主管", "同事", "工作", "加班", "會議", "客戶"))
        {
            return ContainsAny(text, "加班", "壓力", "罵", "煩", "累", "崩潰", "趕", "績效", "deadline")
                ? EventType.WORK_STRESS
                : EventType.WORK;
        }
        if (ContainsAny(text, "家人", "爸", "媽", "家庭", "父母")) return EventType.FAMILY;
        if (ContainsAny(text, "朋友", "閨蜜", "兄弟")) return EventType.FRIENDSHIP;
        if (ContainsAny(text, "曖昧", "分手", "喜歡", "感情", "戀愛")) return EventType.RELATIONSHIP;
        if (ContainsAny(text, "讀書", "考試", "作業", "課", "學習")) return EventType.STUDY;
        if (ContainsAny(text, "錢", "薪水", "房租", "省錢", "花費")) return EventType.MONEY;
        if (ContainsAny(text, "生病", "睡不著", "健康", "醫生", "疲倦")) return EventType.HEALTH;
        if (ContainsAny(text, "開心", "小確幸", "咖啡", "早餐", "散步")) return EventType.SMALL_HAPPINESS;
        return EventType.RANDOM;
    }

    private static List<EmotionTag> ResolveEmotionTags(string text)
    {
        var tags = new List<EmotionTag>();
        if (ContainsAny(text, "累", "疲", "加班", "睡不飽")) tags.Add(EmotionTag.TIRED);
        if (ContainsAny(text, "氣", "火大", "生氣", "怒")) tags.Add(EmotionTag.ANGRY);
        if (ContainsAny(text, "難過", "哭", "失落")) tags.Add(EmotionTag.SAD);
        if (ContainsAny(text, "孤單", "沒人懂", "一個人")) tags.Add(EmotionTag.LONELY);
        if (ContainsAny(text, "焦慮", "擔心", "緊張", "不安")) tags.Add(EmotionTag.ANXIOUS);
        if (ContainsAny(text, "開心", "幸福", "舒服")) tags.Add(EmotionTag.HAPPY);
        if (ContainsAny(text, "期待", "興奮")) tags.Add(EmotionTag.EXCITED);
        if (ContainsAny(text, "委屈", "誤會", "不是我的問題", "不公平")) tags.Add(EmotionTag.WRONGED);
        if (ContainsAny(text, "平靜", "還好", "慢慢來")) tags.Add(EmotionTag.CALM);
        return tags;
    }

    private static List<string> ResolveValueTags(string text)
    {
        var tags = new List<string>();
        if (ContainsAny(text, "不公平", "委屈", "誤會")) tags.Add("FAIRNESS");
        if (ContainsAny(text, "不敢", "壓力", "主管")) tags.Add("RESPECT");
        if (ContainsAny(text, "慢慢", "陪", "聽")) tags.Add("GENTLE_COMPANY");
        if (ContainsAny(text, "省錢", "存錢", "花費")) tags.Add("FINANCIAL_STABILITY");
        return tags.Distinct().ToList();
    }

    private static List<string> ResolveInterestTags(string text)
    {
        var tags = new List<string>();
        if (ContainsAny(text, "電影", "動畫", "影集")) tags.Add("MOVIE");
        if (ContainsAny(text, "咖啡", "早餐")) tags.Add("FOOD");
        if (ContainsAny(text, "散步", "運動", "健身")) tags.Add("EXERCISE");
        if (ContainsAny(text, "程式", "coding", "工程師")) tags.Add("CODING");
        if (ContainsAny(text, "讀書", "學習")) tags.Add("LEARNING");
        return tags.Distinct().ToList();
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(text.Contains);
    }
}
