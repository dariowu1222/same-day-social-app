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

    /// <summary>呼叫者按過這篇的愛心了沒。未登入時一律 false。只說「我」，不說「誰」。</summary>
    public bool IsLiked { get; set; }

    /// <summary>
    /// 作者已經刪掉這篇，剩下的是底下別人的回應。
    ///
    /// true 的時候這個物件只是一個殼：內容、圖片、標籤、作者名字全部是空的
    /// （見 RantService.Assemble），前端在原本貼文的位置顯示「這則貼文已被作者刪除」。
    /// 牆上不會出現這種貼文，只有帶著連結進來才看得到。
    /// </summary>
    public bool IsDeleted { get; set; }

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

    /// <summary>
    /// 前三個回應的人的名字，牆上收合的卡片疊頭像用。
    ///
    /// 只有列表要求摘要（?replies=summary）時才有值，那時候 Replies 是空的、
    /// ReplyCount 仍然是真正的則數。其他情況是 null，而且不會出現在 JSON 裡——
    /// 已經上架的 App 不認得這一欄，也用不到。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ReplyNames { get; set; }

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

    /// <summary>呼叫者按過這則的愛心了沒。未登入時一律 false。</summary>
    public bool IsLiked { get; set; }

    /// <summary>
    /// 這則被貼文作者隱藏了（自己被藏，或是它回的那一則被藏）。
    ///
    /// 只有貼文作者看得到 true——其他人根本拿不到這一則，而回應的人自己拿到的是
    /// false：不告訴他被藏了，那通常只會變成下一輪爭執的起點。
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>可以取消隱藏：是貼文作者親手藏的這一則，不是跟著上一則一起被藏的。</summary>
    public bool CanUnhide { get; set; }

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
