using Microsoft.Extensions.Configuration;

namespace SameDaySocialApp.Application.Services;

/// <summary>
/// 審核後台的權限守門。
///
/// 名單放設定檔（本機用 User Secrets、部署用環境變數），格式為以逗號分隔的 userId：
///   Moderation:AdminUserIds = "user_aaa,user_bbb"
///
/// 設計原則是「預設拒絕」：沒有設定名單時一律不放行。
/// 後台看得到使用者的私訊與貼文，寧可設定漏了而全部擋下，也不要因為漏設而全部放行。
///
/// 之後若要改成 users 表的角色欄位，只需替換這個類別的實作，呼叫端不必更動。
/// </summary>
public sealed class AdminAccessService(IConfiguration configuration)
{
    private readonly HashSet<string> adminIds = (configuration["Moderation:AdminUserIds"] ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.Ordinal);

    public bool IsConfigured => adminIds.Count > 0;

    public bool IsAdmin(string? userId)
        => !string.IsNullOrWhiteSpace(userId) && adminIds.Contains(userId);
}
