using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Domain.Entities;
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
    /// AdminUserService.RestoreExpiredSuspensions，而它要等排程（每小時一次，見
    /// .github/workflows/restore-suspensions.yml）跑過才會動那一列。只信 status 欄位的話，
    /// 中間那段時間人已經期滿了卻還被當成停權中。
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

    /// <summary>
    /// 一批 id 裡現在還能見人的那幾個。
    ///
    /// 列表要把別人的帳號濾掉時走這支（牢騷牆的作者、任務的參與者），不要各自
    /// 再寫一次「載入 users 再比對狀態」——判斷還是上面那個 IsActive，這裡只負責
    /// 把兩種儲存模式的載入收在一處。
    ///
    /// 不經過快取：快取是給請求閘門那種「同一個 id 每個請求都問一次」用的，
    /// 列表一次問的是一整批各不相同的 id，塞進去只會把真正有用的項目洗掉。
    /// </summary>
    public static HashSet<string> ActiveAmong(
        AppDbContext? db, JsonStorageService storage, IReadOnlyCollection<string> userIds, DateTimeOffset now)
        => ActiveNamesAmong(db, storage, userIds, now).Keys.ToHashSet();

    /// <summary>
    /// 同一批人，連同他們「現在」的暱稱。
    ///
    /// 為什麼名字要跟狀態一起查：牢騷牆要顯示的名字和要過濾的狀態，問的是同一批
    /// 使用者——那一頁所有貼文的作者加上所有回覆者。既然為了過濾本來就得跑這支
    /// 查詢，名字多帶一個欄位就好，不必另外再往返一趟。
    ///
    /// 上面的 ActiveAmong 只是丟掉名字的版本。兩支共用同一份載入邏輯，才不會
    /// 哪天其中一邊補了規則、另一邊沒補。
    /// </summary>
    public static Dictionary<string, string> ActiveNamesAmong(
        AppDbContext? db, JsonStorageService storage, IReadOnlyCollection<string> userIds, DateTimeOffset now)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        if (db != null)
        {
            return db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Nickname, u.Status, u.SuspendedUntil })
                .ToList()
                .Where(u => IsActive(u.Status, u.SuspendedUntil, now))
                .ToDictionary(u => u.Id, u => u.Nickname);
        }

        // JSON 檔案儲存模式走同一套規則，包含「查無此人即不可用」。
        // 沒有資料庫時 AuthService 六個入口全部回 DATABASE_NOT_CONFIGURED，發不出
        // token，需要登入的路徑一條都進不來，所以這條分支實際上走不到——正因為
        // 如此更沒有理由替它另立一套寬鬆的判斷，讓兩套相反的規則並存在同一份程式裡。
        //
        // DistinctBy 是這條分支才需要的：資料庫那邊 id 是主鍵，這裡讀的是一份
        // 檔案，重複的話 ToDictionary 會直接丟例外。
        return storage.ReadCollection<User>("users")
            .Where(u => userIds.Contains(u.Id) && IsActive(u.Status, u.SuspendedUntil, now))
            .DistinctBy(u => u.Id)
            .ToDictionary(u => u.Id, u => u.Nickname);
    }

    /// <summary>單筆版。查無此人同樣算不可用。</summary>
    public static bool IsActive(
        AppDbContext? db, JsonStorageService storage, string userId, DateTimeOffset now)
        => !string.IsNullOrWhiteSpace(userId)
           && ActiveAmong(db, storage, [userId], now).Contains(userId);

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
