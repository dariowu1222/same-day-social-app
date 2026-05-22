namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class TodayEntryRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Content { get; set; } = "";
    public string EventType { get; set; } = "RANDOM";
    public string[] EmotionTags { get; set; } = [];
    public string[] ValueTags { get; set; } = [];
    public string[] InterestTags { get; set; } = [];
    public string ResponseMode { get; set; } = "JUST_LISTEN";
    public string Visibility { get; set; } = "MATCH_ONLY";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
