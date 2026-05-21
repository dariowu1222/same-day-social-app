using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Domain.Entities;

public sealed class RantPost
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string Content { get; set; } = "";
    public RantMode Mode { get; set; } = RantMode.JUST_SAYING;
    public List<EmotionTag> EmotionTags { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int LikeCount { get; set; }
    public int ReplyCount { get; set; }
    public bool IsHidden { get; set; }
    public int ReportCount { get; set; }
    public List<RantReply> Replies { get; set; } = [];
}

public sealed class RantReply
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
