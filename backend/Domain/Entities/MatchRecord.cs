using SameDaySocialApp.Domain.Enums;
using MatchType = SameDaySocialApp.Domain.Enums.MatchType;

namespace SameDaySocialApp.Domain.Entities;

public sealed class MatchRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string MatchedUserId { get; set; } = "";
    public int MatchScore { get; set; }
    public MatchType MatchType { get; set; } = MatchType.SAME_EMOTION;
    public List<string> SharedTags { get; set; } = [];
    public string Reason { get; set; } = "";
    public string Icebreaker { get; set; } = "";
    public bool UserLiked { get; set; }
    public bool MatchedUserLiked { get; set; }

    /// <summary>
    /// 左滑略過的時間。null 代表還沒略過過。
    /// 與 UserLiked 一起構成「已表態」：右滑永久排除，左滑只排除冷卻期內。
    /// </summary>
    public DateTimeOffset? PassedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
