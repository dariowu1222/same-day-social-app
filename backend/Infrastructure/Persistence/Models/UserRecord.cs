namespace SameDaySocialApp.Infrastructure.Persistence.Models;

public sealed class UserRecord
{
    public string Id { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string? AgeRange { get; set; }
    public string? Gender { get; set; }
    public string? LocationArea { get; set; }
    public string Bio { get; set; } = "";
    public string[] InterestTags { get; set; } = [];
    public string[] ValueTags { get; set; } = [];
    public string[] PhotoDataUrls { get; set; } = [];
    public string ResponsePreference { get; set; } = "JUST_LISTEN";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
