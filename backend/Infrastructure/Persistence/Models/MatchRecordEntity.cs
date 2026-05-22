namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class MatchRecordEntity
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string MatchedUserId { get; set; } = "";
    public int MatchScore { get; set; }
    public string MatchType { get; set; } = "SAME_EMOTION";
    public string[] SharedTags { get; set; } = [];
    public string Reason { get; set; } = "";
    public string Icebreaker { get; set; } = "";
    public bool UserLiked { get; set; }
    public bool MatchedUserLiked { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
