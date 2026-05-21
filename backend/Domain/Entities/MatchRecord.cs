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
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
