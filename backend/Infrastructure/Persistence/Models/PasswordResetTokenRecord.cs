namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class PasswordResetTokenRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
