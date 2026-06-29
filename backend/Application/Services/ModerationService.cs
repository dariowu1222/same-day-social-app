using System.Text.RegularExpressions;

namespace SameDaySocialApp.Application.Services;

public sealed record ModerationResult(bool IsBlocked, string? Warning, string? Code, string? Message);

public sealed class ModerationService
{
    private static readonly Regex PhoneRegex = new(@"09\d{8}|\d{2,4}-\d{6,8}", RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(@"https?://|www\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IdRegex = new(@"[A-Z][12]\d{8}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ModerationResult Check(string content)
    {
        var text = content.ToLowerInvariant();
        if (PhoneRegex.IsMatch(content) || UrlRegex.IsMatch(content) || IdRegex.IsMatch(content))
        {
            return Blocked();
        }

        // 詞庫來自共用單一來源 shared/lexicon/safety-lexicon.json（改詞請改該檔並重新生成）。
        if (ContainsAny(text, SafetyLexicon.BlockPii) || ContainsAny(text, SafetyLexicon.BlockViolence))
        {
            return Blocked();
        }

        if (ContainsAny(text, SafetyLexicon.WarnEmotion))
        {
            return new ModerationResult(false, "這篇看起來情緒比較強，建議避免出現可辨識人物或公司資訊。", null, null);
        }

        return new ModerationResult(false, null, null, null);
    }

    private static ModerationResult Blocked()
    {
        return new ModerationResult(
            true,
            null,
            "CONTENT_BLOCKED",
            "這篇內容可能包含個資、攻擊或高風險內容，請調整後再發布。");
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(text.Contains);
    }
}
