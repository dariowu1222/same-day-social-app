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
    public string? DatingGoal { get; set; }
    public string? LookingFor { get; set; }
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public int? DistanceKm { get; set; }
    public string? PreferredArea { get; set; }
    public string[] Languages { get; set; } = [];
    public string? ActiveTime { get; set; }
    public bool VoiceFirst { get; set; }
    public bool MeetSoon { get; set; }
    // ── 管理端欄位 ──
    public string Status { get; set; } = "ACTIVE";
    public DateTimeOffset? SuspendedUntil { get; set; }
    public string? StatusReason { get; set; }
    // 付費機制尚未開發：欄位先備妥，目前一律為 FREE
    public string MembershipTier { get; set; } = "FREE";
    public DateTimeOffset? MembershipExpiresAt { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    public bool IdentityVerified { get; set; }
    public DateTimeOffset? LastActiveAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

// 隱私 / 通知設定（每使用者一列）
public sealed class UserSettingRecord
{
    public string UserId { get; set; } = "";
    public bool ProfilePublic { get; set; } = true;
    public bool PauseMatching { get; set; }
    public bool NotifyMatch { get; set; } = true;
    public bool NotifyMessage { get; set; } = true;
    public bool NotifyRant { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
