namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class AuthAccountRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool IsDisabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
