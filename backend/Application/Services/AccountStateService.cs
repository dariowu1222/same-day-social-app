using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Application.Services;

/// <summary>
/// 「這個帳號現在還能用嗎」的即時判斷。
///
/// 為什麼需要這個東西：
/// 封鎖／停權／註銷原本只把 auth_accounts.is_disabled 設成 true，而那個欄位
/// 只有登入路徑會讀。所有 [Authorize] 端點都只驗 JWT 簽章，沒有任何一處回頭
/// 查帳號狀態——所以被封鎖的人手上那顆 token 最長還能用七天，繼續傳訊息、
/// 讀對話。「永久封鎖」對正在騷擾人的帳號等於沒有作用，而那正是最需要它生效的時候。
///
/// 為什麼用快取而不是每次查資料庫：
/// 資料庫在 Supabase，一次往返 40-60ms。每個請求都查等於把全站 API 的延遲
/// 墊高一截，而帳號狀態一天也變不了幾次，是典型「讀很多、寫極少」。
///
/// 為什麼 60 秒還能算「即時」：
/// 處置發生時 ChangeStatus 會直接把那個人的快取清掉，所以從後台按下去是當場生效。
/// 60 秒的存活時間只是給「繞過後台直接改資料庫」「停權到期自動恢復」這類
/// 帶外變更的上限，不是正常路徑的延遲。
/// </summary>
public sealed class AccountStateService(IServiceScopeFactory scopes, IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private static string Key(string userId) => $"acct-active:{userId}";

    /// <summary>
    /// 「這個狀態現在還算可用嗎」的單一判斷。任何要依帳號狀態放行或遮蔽的地方
    /// （請求閘門、配對候選池）都走這裡，不要各自寫一份——兩套判斷遲早會分岔，
    /// 而分岔的那一邊通常是漏的那一邊。
    ///
    /// 為什麼要自己看 suspended_until：
    /// 停權是「暫時」的，期限一到就該回到正常狀態。真正把 users.status 寫回 ACTIVE 的是
    /// AdminUserService.RestoreExpiredSuspensions，但它只在後台開啟會員列表時才跑，
    /// 沒有任何排程。只信 status 欄位的話，沒人開後台就等於永久停權。
    /// </summary>
    public static bool IsActive(AccountStatus status, DateTimeOffset? suspendedUntil, DateTimeOffset now)
    {
        if (status == AccountStatus.ACTIVE) return true;

        // BANNED／DELETED 沒有期限，不會因為時間過去而恢復
        return status == AccountStatus.SUSPENDED && suspendedUntil != null && suspendedUntil <= now;
    }

    /// <summary>資料庫欄位（varchar）版本。認不得的值一律視為不可用。</summary>
    public static bool IsActive(string? status, DateTimeOffset? suspendedUntil, DateTimeOffset now)
        => Enum.TryParse<AccountStatus>(status, out var parsed)
           && IsActive(parsed, suspendedUntil, now);

    public async Task<bool> IsActiveAsync(string userId, CancellationToken ct)
    {
        if (cache.TryGetValue<bool>(Key(userId), out var cached))
        {
            return cached;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetService<AppDbContext>();
        if (db == null)
        {
            // JSON 檔案儲存模式（本機沒設連線字串）不做這個檢查
            return true;
        }

        var row = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Status, u.SuspendedUntil })
            .FirstOrDefaultAsync(ct);

        // 查無此人也算不可用：token 指向一個不存在的帳號，沒有理由放行
        var active = row != null && IsActive(row.Status, row.SuspendedUntil, DateTimeOffset.UtcNow);
        cache.Set(Key(userId), active, Ttl);
        return active;
    }

    /// <summary>狀態變更後呼叫，讓下一個請求立刻看到新狀態。</summary>
    public void Invalidate(string userId) => cache.Remove(Key(userId));
}
