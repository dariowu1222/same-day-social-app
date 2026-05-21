using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Domain.Entities;

public sealed class TodayEntry
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Content { get; set; } = "";
    public EventType EventType { get; set; } = EventType.RANDOM;
    public List<EmotionTag> EmotionTags { get; set; } = [];
    public List<string> ValueTags { get; set; } = [];
    public List<string> InterestTags { get; set; } = [];
    public ResponseMode ResponseMode { get; set; } = ResponseMode.JUST_LISTEN;
    public Visibility Visibility { get; set; } = Visibility.MATCH_ONLY;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
