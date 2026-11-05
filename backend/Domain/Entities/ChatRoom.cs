namespace SameDaySocialApp.Domain.Entities;

public sealed class ChatRoom
{
    public string Id { get; set; } = "";
    public List<string> UserIds { get; set; } = [];
    public string SourceType { get; set; } = "TODAY_MATCH";
    public string SourceId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 閒置到期被鎖定的時間。非 null 代表這間房只剩「退出聊天室」：
    /// 還在列表上、看得到歷史訊息，但不能再傳送。
    /// </summary>
    public DateTimeOffset? ExpiredAt { get; set; }
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

    /// <summary>釘選的時間，null 代表沒釘。聊天列表用它決定釘選區的先後。</summary>
    public DateTimeOffset? PinnedAt { get; set; }
}
