namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class PasswordResetTokenRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>這組碼被猜錯幾次。到 AuthSecurity.MaxCodeAttempts 就連同 UsedAt 一起作廢。</summary>
    public int FailedAttempts { get; set; }
}
