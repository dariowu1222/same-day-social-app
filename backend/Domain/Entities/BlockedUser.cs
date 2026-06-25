namespace SameDaySocialApp.Domain.Entities;

public sealed class BlockedUser
{
    public string UserId { get; set; } = "";
    public string Nickname { get; set; } = "";
    public DateTimeOffset BlockedAt { get; set; }
}

public sealed class UserSetting
{
    public bool ProfilePublic { get; set; } = true;
    public bool PauseMatching { get; set; }
    public bool NotifyMatch { get; set; } = true;
    public bool NotifyMessage { get; set; } = true;
    public bool NotifyRant { get; set; } = true;
}
