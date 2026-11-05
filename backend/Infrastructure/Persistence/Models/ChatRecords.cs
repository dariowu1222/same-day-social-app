namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class ChatRoomRecord
{
    public string Id { get; set; } = "";
    public string SourceType { get; set; } = "TODAY_MATCH";
    public string SourceId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>已經寄出「即將到期」通知的時間。沒寄過就不准鎖定。</summary>
    public DateTimeOffset? ExpiryNotifiedAt { get; set; }

    /// <summary>
    /// 鎖定時間。非 null 代表這間房只剩「退出聊天室」一個動作：
    /// 還在列表上、進得去、看得到歷史，但不能再送訊息。
    /// 真正離開由本人按下去，排程不代勞。
    /// </summary>
    public DateTimeOffset? ExpiredAt { get; set; }
}

/// <summary>
/// 後台可調的營運規則。放資料庫而不是 appsettings，是因為後者要重新部署才生效。
/// 值一律存字串，型別與合法範圍由 AppSettingsService 把關。
/// </summary>
public sealed class AppSettingRecord
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
}

public sealed class ChatRoomUserRecord
{
    public string Id { get; set; } = "";
    public string ChatRoomId { get; set; } = "";
    public string UserId { get; set; } = "";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>已讀水位。null 代表從未標記過，計算未讀時退回 JoinedAt。</summary>
    public DateTimeOffset? LastReadAt { get; set; }
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

    /// <summary>
    /// 釘選的時間，用來決定釘選區的排序。
    /// 不能用 UpdatedAt 代替——改備註名或靜音也會動到它，順序就會被打亂。
    /// </summary>
    public DateTimeOffset? PinnedAt { get; set; }

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
