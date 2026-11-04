using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 關鍵字分析器對「工作」的判斷。
///
/// 「上班／主管／同事／工作／加班／會議／客戶」以前全部直接判成 WORK_STRESS，
/// 所以「今天上班順手把案子做完了」會被標成工作壓力——標籤和事實相反，
/// 然後拿去跟真的在工作上受挫的人加 30 分（事件相同是全部計分項裡最重的）。
/// </summary>
public sealed class TodayAnalyzerEventTypeTests
{
    private readonly TodayAnalyzerService analyzer = new();

    private EventType EventOf(string text)
        => analyzer.Analyze(text, ResponseMode.JUST_LISTEN).EventType;

    [Theory]
    [InlineData("今天上班順手把案子做完了，還提早下班")]
    [InlineData("早上開了個會，下午跟客戶對完需求")]
    [InlineData("今天同事帶了伴手禮來公司")]
    public void NeutralWorkTalkIsWork(string text)
        => Assert.Equal(EventType.WORK, EventOf(text));

    [Theory]
    [InlineData("今天上班被主管罵，加班到十點")]
    [InlineData("工作壓力好大，快崩潰了")]
    [InlineData("會議一個接一個，累死")]
    public void WorkWithStressSignalIsWorkStress(string text)
        => Assert.Equal(EventType.WORK_STRESS, EventOf(text));

    /// <summary>
    /// 沒提到工作的句子不該被這條規則碰到。
    /// 工作是整條 if 鏈的第一個判斷，擴大它的範圍會把後面的類型都吃掉。
    /// </summary>
    [Theory]
    [InlineData("今天跟家人吃飯", EventType.FAMILY)]
    [InlineData("明天要考試，有點緊張", EventType.STUDY)]
    [InlineData("散步的時候看到很漂亮的夕陽", EventType.SMALL_HAPPINESS)]
    public void NonWorkTextIsUnaffected(string text, EventType expected)
        => Assert.Equal(expected, EventOf(text));
}
