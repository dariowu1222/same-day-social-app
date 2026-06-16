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
    public string? QuotedMessageId { get; set; }
    public string? QuotedSenderName { get; set; }
    public string? QuotedContent { get; set; }
    public bool IsRecalled { get; set; }
}

// 每位使用者對某對話的個人設定（備註名 / 釘選 / 靜音）
public sealed class ChatMemberSettingRecord
{
    public string Id { get; set; } = "";
    public string ChatRoomId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? NoteName { get; set; }
    public bool Pinned { get; set; }
    public bool Muted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UserBlockRecord
{
    public string Id { get; set; } = "";
    public string BlockerId { get; set; } = "";
    public string BlockedId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ChatReportRecord
{
    public string Id { get; set; } = "";
    public string ReporterId { get; set; } = "";
    public string ReportedUserId { get; set; } = "";
    public string ChatRoomId { get; set; } = "";
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
