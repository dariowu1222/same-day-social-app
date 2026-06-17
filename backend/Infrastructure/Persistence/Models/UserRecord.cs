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
    public DateOnly? Birthday { get; set; }
    public string ResponsePreference { get; set; } = "JUST_LISTEN";
    public string? Relationship { get; set; }
    public string[] PersonalityTags { get; set; } = [];
    public string[] AppearanceTags { get; set; } = [];
    public int? Height { get; set; }
    public int? Weight { get; set; }
    public string? Occupation { get; set; }
    public string? School { get; set; }
    public string? BloodType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
