namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class ChatRoomRecord
{
    public string Id { get; set; } = "";
    public string SourceType { get; set; } = "TODAY_MATCH";
    public string SourceId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ChatRoomUserRecord
{
    public string Id { get; set; } = "";
    public string ChatRoomId { get; set; } = "";
    public string UserId { get; set; } = "";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ChatMessageRecord
{
    public string Id { get; set; } = "";
    public string ChatRoomId { get; set; } = "";
    public string SenderId { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
