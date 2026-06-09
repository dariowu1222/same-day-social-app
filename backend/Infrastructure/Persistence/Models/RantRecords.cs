namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class RantPostRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string Content { get; set; } = "";
    public string Mode { get; set; } = "JUST_SAYING";
    public string[] EmotionTags { get; set; } = [];
    public string[] HashTags { get; set; } = [];
    public string? ImageDataUrl { get; set; }
    public string? AudioDataUrl { get; set; }
    public bool IsHidden { get; set; }
    public int ReportCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RantReplyRecord
{
    public string Id { get; set; } = "";
    public string RantPostId { get; set; } = "";
    public string? ParentReplyId { get; set; }
    public string UserId { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ImageDataUrl { get; set; }
    public string? AudioDataUrl { get; set; }
    public int LikeCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RantReactionRecord
{
    public string Id { get; set; } = "";
    public string RantPostId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string ReactionType { get; set; } = "UNDERSTAND";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RantReportRecord
{
    public string Id { get; set; } = "";
    public string RantPostId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
