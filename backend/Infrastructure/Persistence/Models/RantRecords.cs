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
    public string[] ImageUrls { get; set; } = [];
    public string[] ThumbUrls { get; set; } = [];
    public string? ImageUrl { get; set; }
    public string? AudioUrl { get; set; }
    public bool IsHidden { get; set; }
    public int ReportCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 有值＝作者刪了這篇。列留著 30 天給後台查，之後由 RantService.PurgeDeleted 清掉。
    /// 跟 IsHidden 是兩回事：IsHidden 是「被處置」，這個是「作者自己不要了」。
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class RantReplyRecord
{
    public string Id { get; set; } = "";
    public string RantPostId { get; set; } = "";
    public string? ParentReplyId { get; set; }
    public string UserId { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? ThumbUrl { get; set; }
    public string? AudioUrl { get; set; }
    public int LikeCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>有值＝回應的人自己收回了。對所有人隱形，列留著 30 天給後台查。</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>有值＝被隱藏。誰藏的、誰還看得到，看 HiddenBy。</summary>
    public DateTimeOffset? HiddenAt { get; set; }

    /// <summary>RantReplyHiddenBy.Author 或 .Admin。HiddenAt 是 null 時這欄也是 null。</summary>
    public string? HiddenBy { get; set; }
}

/// <summary>rant_replies.hidden_by 的兩個值。</summary>
public static class RantReplyHiddenBy
{
    /// <summary>貼文作者藏的：其他人看不到；作者看得到、可以取消；回應的人自己還看得到。</summary>
    public const string Author = "AUTHOR";

    /// <summary>後台處理檢舉時藏的：所有人都看不到，貼文作者也不能取消。</summary>
    public const string Admin = "ADMIN";
}

public sealed class RantReactionRecord
{
    public string Id { get; set; } = "";
    public string RantPostId { get; set; } = "";
    /// <summary>
    /// 有值＝這個讚是按在某一則回覆上，null＝按在貼文本身。
    /// 兩種都留著 RantPostId，所以仍然查得到這個讚屬於哪一篇。
    /// </summary>
    public string? RantReplyId { get; set; }
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
