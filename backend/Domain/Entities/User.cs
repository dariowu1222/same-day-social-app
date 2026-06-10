using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Domain.Entities;

public sealed class User
{
    public string Id { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string? AgeRange { get; set; }
    public string? Gender { get; set; }
    public string? LocationArea { get; set; }
    public string Bio { get; set; } = "";
    public List<string> InterestTags { get; set; } = [];
    public List<string> ValueTags { get; set; } = [];
    public List<string> PhotoDataUrls { get; set; } = [];
    public ResponseMode ResponsePreference { get; set; } = ResponseMode.JUST_LISTEN;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
