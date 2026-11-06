using System.Text.Json.Serialization;
using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Domain.Entities;

public sealed class RantPost
{
    public string Id { get; set; } = "";

    /// <summary>
    /// 不往外送。
    ///
    /// GET /api/rants 與 GET /api/rants/{id} 沒有 [Authorize]，任何人都讀得到，
    /// 而回應裡原本帶著作者的 user id——匿名發文於是可以被對應回某一個帳號，
    /// 再拿那個 id 去 /api/profile/{userId} 查出這個人是誰。整面牆的匿名性
    /// 就是在這一欄上漏掉的。
    ///
    /// 前端需要的其實只有「這篇是不是我發的」（決定要不要顯示刪除選單），
    /// 那是 IsMine。
    /// </summary>
    [JsonIgnore]
    public string UserId { get; set; } = "";

    /// <summary>這篇是不是呼叫者自己發的。未登入時一律 false。</summary>
    public bool IsMine { get; set; }

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

    // 這兩欄是審核狀態，不是給讀者看的。回出去等於在公開牆上寫著
    // 「這篇已經被兩個人檢舉了」。回傳的貼文 IsHidden 必定為 false
    // （隱藏的根本查不到），帶出去只是白白多說一件事。
    [JsonIgnore]
    public bool IsHidden { get; set; }

    [JsonIgnore]
    public int ReportCount { get; set; }

    public List<RantReply> Replies { get; set; } = [];
}

public sealed class RantReply
{
    public string Id { get; set; } = "";

    /// <summary>不往外送，理由同 RantPost.UserId。</summary>
    [JsonIgnore]
    public string UserId { get; set; } = "";

    /// <summary>這則是不是呼叫者自己回的。未登入時一律 false。</summary>
    public bool IsMine { get; set; }

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
