namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class SocialTaskRecord
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Difficulty { get; set; } = "EASY";
    public int ParticipantLimit { get; set; } = 2;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TaskParticipantRecord
{
    public string Id { get; set; } = "";
    public string TaskId { get; set; } = "";
    public string UserId { get; set; } = "";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}
