namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class NotificationRecord
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
