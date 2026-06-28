namespace SameDaySocialApp.Domain.Entities;

// 站內通知（每位收件者一列）。type 目前有 LIKE（被按讚/有共鳴）、RANT_REPLY（樹洞被回覆）。
// linkType/linkId 供前端點擊跳轉：match→配對頁、rant→樹洞詳情。
public sealed class Notification
{
    public string Id { get; set; } = "";
    public string RecipientId { get; set; } = "";
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? LinkType { get; set; }
    public string? LinkId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
