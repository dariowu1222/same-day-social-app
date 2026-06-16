namespace SameDaySocialApp.Domain.Entities;

public sealed class ChatRoom
{
    public string Id { get; set; } = "";
    public List<string> UserIds { get; set; } = [];
    public string SourceType { get; set; } = "TODAY_MATCH";
    public string SourceId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ChatMessage
{
    public string Id { get; set; } = "";
    public string ChatRoomId { get; set; } = "";
    public string SenderId { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? QuotedMessageId { get; set; }
    public string? QuotedSenderName { get; set; }
    public string? QuotedContent { get; set; }
    public bool IsRecalled { get; set; }
}

public sealed class ChatMemberSetting
{
    public string ChatRoomId { get; set; } = "";
    public string? NoteName { get; set; }
    public bool Pinned { get; set; }
    public bool Muted { get; set; }
}
