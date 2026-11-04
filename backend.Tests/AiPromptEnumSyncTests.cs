using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// AiTodayAnalyzer 的 JSON schema 是用 Enum.GetNames 產生的，加一個 enum 值
/// 就自動變成模型的合法輸出；但 prompt 裡那份選項清單是手寫的，不會跟著更新。
///
/// 兩者脫鉤的症狀很難發現：模型要嘛永遠不用那個新值（看起來像沒生效），
/// 要嘛自己猜它代表什麼（分類漂移，而且事後查不出是哪次改的）。編譯器不會擋，
/// 線上也不會報錯，所以用測試擋。
/// </summary>
public sealed class AiPromptEnumSyncTests
{
    [Fact]
    public void Prompt_lists_every_event_type()
    {
        var missing = Enum.GetNames<EventType>()
            .Where(name => !AiTodayAnalyzer.DefaultSystemPrompt.Contains(name, StringComparison.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0,
            $"EventType 有值沒寫進 AiTodayAnalyzer 的 prompt：{string.Join(", ", missing)}。"
            + "schema 會自動接受它，但模型不知道它是什麼意思。");
    }

    [Fact]
    public void Prompt_lists_every_emotion_tag()
    {
        var missing = Enum.GetNames<EmotionTag>()
            .Where(name => !AiTodayAnalyzer.DefaultSystemPrompt.Contains(name, StringComparison.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0,
            $"EmotionTag 有值沒寫進 AiTodayAnalyzer 的 prompt：{string.Join(", ", missing)}。");
    }
}
