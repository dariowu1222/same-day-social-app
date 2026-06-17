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
    public DateOnly? Birthday { get; set; }
    public ResponseMode ResponsePreference { get; set; } = ResponseMode.JUST_LISTEN;
    // 個人資料欄位（性別必填，其餘可空）
    public string? Relationship { get; set; }
    public List<string> PersonalityTags { get; set; } = [];
    public List<string> AppearanceTags { get; set; } = [];
    public int? Height { get; set; }
    public int? Weight { get; set; }
    public string? Occupation { get; set; }
    public string? School { get; set; }
    public string? BloodType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
