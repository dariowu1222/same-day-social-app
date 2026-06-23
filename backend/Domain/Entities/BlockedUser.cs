namespace SameDaySocialApp.Domain.Entities;

public sealed class BlockedUser
{
    public string UserId { get; set; } = "";
    public string Nickname { get; set; } = "";
    public DateTimeOffset BlockedAt { get; set; }
}
