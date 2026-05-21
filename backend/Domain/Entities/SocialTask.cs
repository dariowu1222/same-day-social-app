using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Domain.Entities;

public sealed class SocialTask
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public TaskCategory Category { get; set; }
    public string Duration { get; set; } = "";
    public string Difficulty { get; set; } = "EASY";
    public int ParticipantLimit { get; set; } = 2;
    public List<string> ParticipantUserIds { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
