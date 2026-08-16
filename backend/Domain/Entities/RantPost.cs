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
    public List<string> HashTags { get; set; } = [];
    // 多圖：貼文可帶多張圖片（Threads 風）。ImageUrl 保留為第一張，維持舊資料/舊前端相容。
    // 存的是物件儲存的公開 URL（非 data URL），DB 欄位仍為 image_data_urls 等歷史名稱。
    public List<string> ImageUrls { get; set; } = [];
    // 縮圖 URL，與 ImageUrls 同索引對齊；列表只載縮圖省頻寬，點開才載原圖。
    // 舊資料沒有縮圖時為空，前端會自動回退成原圖。
    public List<string> ThumbUrls { get; set; } = [];
    public string? ImageUrl { get; set; }
    public string? AudioUrl { get; set; }
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
    public string? ImageUrl { get; set; }
    public string? ThumbUrl { get; set; }
    public string? AudioUrl { get; set; }
    public string? ParentReplyId { get; set; }
    public int LikeCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<RantReply> Replies { get; set; } = [];
}
