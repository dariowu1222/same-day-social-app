namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class RegistrationVerificationTokenRecord
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? BirthYear { get; set; }
    public string? Gender { get; set; }
    public string CodeHash { get; set; } = "";
    public DateTimeOffset TermsAcceptedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
