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
    // 交友意圖（配對品質用，皆可空）
    public string? DatingGoal { get; set; }       // 交友/約會/長期關係/先聊聊
    public string? LookingFor { get; set; }        // 想認識的對象
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public int? DistanceKm { get; set; }
    public string? PreferredArea { get; set; }     // 偏好縣市（與 DistanceKm 二擇一：依距離或依縣市）
    public List<string> Languages { get; set; } = [];
    public string? ActiveTime { get; set; }        // 活動時段
    public bool VoiceFirst { get; set; }           // 可否語音先聊
    public bool MeetSoon { get; set; }             // 可否線下快見
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
