using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Application.Services;

/// <summary>
/// 後台的會員管理。
///
/// 狀態同步原則：users.status（加上 suspended_until）是唯一的真相，登入、請求閘門
/// 與列表過濾全部問 AccountStateService.IsActive。auth_accounts.is_disabled 仍然同步寫，
/// 但只是那份狀態的鏡子，沒有任何判斷依賴它——它是處置當下的快照，不會隨時間變，
/// 曾經讓停權期滿的人舊 token 還能用、重新登入卻被擋下。
/// </summary>
public sealed class AdminUserService
{
    private readonly AppDbContext? db;
    private readonly AccountStateService? accountState;

    public AdminUserService(IServiceProvider services)
    {
        db = services.GetService<AppDbContext>();
        accountState = services.GetService<AccountStateService>();
    }

    public sealed record UserSummary(
        string Id, string Nickname, string Status, string MembershipTier,
        bool EmailVerified, bool IdentityVerified,
        DateTimeOffset? SuspendedUntil, DateTimeOffset CreatedAt,
        DateTimeOffset? LastLoginAt, int ReportsAgainst);

    public sealed record UserDetail(
        string Id, string Nickname, string? Gender, string? AgeRange, string Bio,
        string? LocationArea, string? Occupation, string? School,
        string Status, string? StatusReason, DateTimeOffset? SuspendedUntil,
        string MembershipTier, DateTimeOffset? MembershipExpiresAt,
        bool EmailVerified, bool PhoneVerified, bool IdentityVerified,
        DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt, string? Username,
        int PostCount, int ReplyCount, int MessageCount, int ReportsAgainst,
        List<SanctionEntry> Sanctions,
        List<ReportEntry> Reports);

    /// <summary>
    /// 針對這個會員的單筆檢舉。處置之前必須看得到「被檢舉了什麼」，
    /// 只給次數等於要審核的人憑空判斷。
    /// </summary>
    public sealed record ReportEntry(
        string Id, string TargetType, string TargetId, string Category, int Severity,
        string Status, string? Detail, string? ContextSnapshot,
        DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt, string? Resolution);

    public sealed record SanctionEntry(
        string Action, string? Reason, DateTimeOffset? ExpiresAt,
        string ActorId, DateTimeOffset CreatedAt);

    public sealed record Dashboard(
        int TotalUsers, int NewUsersToday, int SuspendedUsers, int BannedUsers,
        int PendingReports, int HighSeverityReports);

    /// <summary>會員列表。keyword 比對暱稱或 id，支援狀態篩選與分頁。</summary>
    public (List<UserSummary> Items, int Total) Search(
        string? keyword, AccountStatus? status, int page, int pageSize)
    {
        if (db == null) return ([], 0);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            var like = "%" + k + "%";
            query = query.Where(u => EF.Functions.ILike(u.Nickname, like) || u.Id == k);
        }
        if (status.HasValue)
        {
            var s = status.Value.ToString();
            query = query.Where(u => u.Status == s);
        }

        var total = query.Count();
        var rows = query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // 逐筆去查會變成 N+1，改成一次撈這頁需要的關聯資料
        var ids = rows.Select(r => r.Id).ToList();
        var logins = db.AuthAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.UserId))
            .Select(a => new { a.UserId, a.LastLoginAt })
            .ToList()
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.LastLoginAt));

        var dismissed = ReportStatus.DISMISSED.ToString();
        var reportCounts = db.Reports.AsNoTracking()
            .Where(r => ids.Contains(r.ReportedUserId) && r.Status != dismissed)
            .GroupBy(r => r.ReportedUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToList()
            .ToDictionary(x => x.UserId, x => x.Count);

        var items = rows.Select(u => new UserSummary(
            u.Id, u.Nickname, u.Status, u.MembershipTier,
            u.EmailVerified, u.IdentityVerified,
            u.SuspendedUntil, u.CreatedAt,
            logins.GetValueOrDefault(u.Id),
            reportCounts.GetValueOrDefault(u.Id, 0))).ToList();

        return (items, total);
    }

    public UserDetail? GetDetail(string userId)
    {
        if (db == null) return null;
        var u = db.Users.AsNoTracking().FirstOrDefault(x => x.Id == userId);
        if (u == null) return null;

        var account = db.AuthAccounts.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.LastLoginAt)
            .FirstOrDefault();

        var dismissed = ReportStatus.DISMISSED.ToString();

        var sanctions = db.AccountSanctions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .ToList()
            .Select(s => new SanctionEntry(s.Action, s.Reason, s.ExpiresAt, s.ActorId, s.CreatedAt))
            .ToList();

        // 被檢舉明細：含已駁回的也一併呈現，審核時才看得出是真的有問題，
        // 還是被同一群人反覆濫檢舉。
        var reports = db.Reports.AsNoTracking()
            .Where(r => r.ReportedUserId == userId)
            .OrderByDescending(r => r.Severity)
            .ThenByDescending(r => r.CreatedAt)
            .Take(50)
            .ToList()
            .Select(r => new ReportEntry(
                r.Id, r.TargetType, r.TargetId, r.Category, r.Severity,
                r.Status, r.Detail, r.ContextSnapshot,
                r.CreatedAt, r.ReviewedAt, r.Resolution))
            .ToList();

        return new UserDetail(
            u.Id, u.Nickname, u.Gender, u.AgeRange, u.Bio,
            u.LocationArea, u.Occupation, u.School,
            u.Status, u.StatusReason, u.SuspendedUntil,
            u.MembershipTier, u.MembershipExpiresAt,
            u.EmailVerified, u.PhoneVerified, u.IdentityVerified,
            u.CreatedAt, account?.LastLoginAt, account?.Username,
            db.RantPosts.AsNoTracking().Count(p => p.UserId == userId),
            db.RantReplies.AsNoTracking().Count(r => r.UserId == userId),
            db.ChatMessages.AsNoTracking().Count(m => m.SenderId == userId),
            db.Reports.AsNoTracking().Count(r => r.ReportedUserId == userId && r.Status != dismissed),
            sanctions,
            reports);
    }

    public Dashboard GetDashboard()
    {
        if (db == null) return new Dashboard(0, 0, 0, 0, 0, 0);

        var todayStart = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
        var pending = ReportStatus.PENDING.ToString();
        var suspended = AccountStatus.SUSPENDED.ToString();
        var banned = AccountStatus.BANNED.ToString();

        return new Dashboard(
            db.Users.AsNoTracking().Count(),
            db.Users.AsNoTracking().Count(u => u.CreatedAt >= todayStart),
            db.Users.AsNoTracking().Count(u => u.Status == suspended),
            db.Users.AsNoTracking().Count(u => u.Status == banned),
            db.Reports.AsNoTracking().Count(r => r.Status == pending),
            db.Reports.AsNoTracking().Count(r => r.Status == pending && r.Severity >= 80));
    }

    /// <summary>後台修改會員資料（例如違規的暱稱或自介）。</summary>
    public bool UpdateProfile(
        string userId, string actorId,
        string? nickname, string? bio, MembershipTier? tier, DateTimeOffset? membershipExpiresAt)
    {
        if (db == null) return false;
        var u = db.Users.FirstOrDefault(x => x.Id == userId);
        if (u == null) return false;

        if (nickname != null) u.Nickname = nickname.Trim();
        if (bio != null) u.Bio = bio.Trim();
        if (tier.HasValue) u.MembershipTier = tier.Value.ToString();
        if (membershipExpiresAt.HasValue) u.MembershipExpiresAt = membershipExpiresAt;
        u.UpdatedAt = DateTimeOffset.UtcNow;

        db.SaveChanges();
        AddSanction(userId, actorId, SanctionAction.EDIT_PROFILE, "後台修改資料", null, null);
        return true;
    }

    /// <summary>停權。until 為 null 表示沒有到期日，需人工解除。</summary>
    public bool Suspend(string userId, string actorId, string? reason, DateTimeOffset? until, string? reportId)
        => ChangeStatus(userId, actorId, AccountStatus.SUSPENDED, reason, until,
            SanctionAction.SUSPEND, reportId);

    public bool Unsuspend(string userId, string actorId, string? reason)
        => ChangeStatus(userId, actorId, AccountStatus.ACTIVE, reason, null,
            SanctionAction.UNSUSPEND, null);

    public bool Ban(string userId, string actorId, string? reason, string? reportId)
        => ChangeStatus(userId, actorId, AccountStatus.BANNED, reason, null,
            SanctionAction.BAN, reportId);

    public bool Unban(string userId, string actorId, string? reason)
        => ChangeStatus(userId, actorId, AccountStatus.ACTIVE, reason, null,
            SanctionAction.UNBAN, null);

    /// <summary>
    /// 註銷帳號。刻意不在此清除個資：
    /// 管理端註銷多半伴隨爭議或申訴，資料清掉就失去佐證。
    /// 這裡只停用登入並標記 DELETED，個資的實際清除另依保留政策處理
    /// （使用者自助刪除的流程仍會清個資，兩者用途不同）。
    /// </summary>
    public bool MarkDeleted(string userId, string actorId, string? reason, string? reportId)
        => ChangeStatus(userId, actorId, AccountStatus.DELETED, reason, null,
            SanctionAction.DELETE, reportId);

    private bool ChangeStatus(
        string userId, string actorId, AccountStatus status, string? reason,
        DateTimeOffset? until, SanctionAction action, string? reportId)
    {
        if (db == null) return false;
        var u = db.Users.FirstOrDefault(x => x.Id == userId);
        if (u == null) return false;

        var now = DateTimeOffset.UtcNow;

        u.Status = status.ToString();
        u.StatusReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        u.SuspendedUntil = until;
        u.UpdatedAt = now;

        // 要不要停掉登入，從狀態本身算出來，不另外由呼叫端傳一個布林進來。
        // 傳進來的話等於同一件事有兩個說法，而那兩個說法只要有一次對不上
        // （例如新增一種處置時把參數抄錯），後台顯示的狀態和當事人實際遇到的
        // 就會分岔，偏偏這種落差最難從回報裡看出來。
        var disableLogin = !AccountStateService.IsActive(status, until, now);

        // 鏡子要跟著一起寫。擋登入的已經不是它了，但直接查資料庫的人會看這個欄位，
        // 放著不動等於在資料庫裡留一個跟 users.status 矛盾的值給下一個人誤判。
        foreach (var account in db.AuthAccounts.Where(a => a.UserId == userId))
        {
            account.IsDisabled = disableLogin;
            account.UpdatedAt = now;
        }

        // 停用登入只擋「下次登入」。如果這個人手上有還活著的後台 session，
        // 不撤銷的話他照樣能用到 8 小時上限——而被處置的人剛好最可能是
        // 「帳號被盜、現在正在用後台」的那個。資料庫層級直接撤，不繞服務。
        if (disableLogin)
        {
            db.AdminSessions
                .Where(x => x.UserId == userId && x.RevokedAt == null)
                .ExecuteUpdate(set => set.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow));
        }

        db.SaveChanges();

        // 狀態變了就把快取清掉，讓下一個請求立刻看到。沒有這一行的話，
        // 處置要等快取過期才生效，而那正是最不該有延遲的時刻。
        accountState?.Invalidate(userId);

        AddSanction(userId, actorId, action, reason, until, reportId);
        return true;
    }

    /// <summary>
    /// 停權到期自動恢復。設了期限就該自己失效，
    /// 否則只要沒人記得手動解除，暫時停權就變成永久停權。
    ///
    /// 由排程每小時打 /api/maintenance/restore-expired-suspensions 觸發，
    /// 管理員開後台儀表板時也會順手跑一次。放行與否不靠這支——登入和讀取都自己
    /// 判斷期限——這支負責的是讓資料庫裡的 users.status 不要長期跟事實分岔，
    /// 因為後台的列表、篩選與計數是直接查那個欄位的。
    /// </summary>
    public int RestoreExpiredSuspensions()
    {
        if (db == null) return 0;
        var now = DateTimeOffset.UtcNow;
        var suspended = AccountStatus.SUSPENDED.ToString();

        var expired = db.Users
            .Where(u => u.Status == suspended && u.SuspendedUntil != null && u.SuspendedUntil <= now)
            .ToList();
        if (expired.Count == 0) return 0;

        foreach (var u in expired)
        {
            u.Status = AccountStatus.ACTIVE.ToString();
            u.SuspendedUntil = null;
            u.StatusReason = null;
            u.UpdatedAt = now;

            foreach (var account in db.AuthAccounts.Where(a => a.UserId == u.Id))
            {
                account.IsDisabled = false;
                account.UpdatedAt = now;
            }

            AddSanctionEntity(u.Id, "system", SanctionAction.UNSUSPEND, "停權期滿自動恢復", null, null);
        }

        db.SaveChanges();

        // 停權到期恢復也要清快取，否則人已經解禁了卻還要等快取過期才能用
        foreach (var u in expired)
        {
            accountState?.Invalidate(u.Id);
        }

        return expired.Count;
    }

    private void AddSanction(
        string userId, string actorId, SanctionAction action,
        string? reason, DateTimeOffset? expiresAt, string? reportId)
    {
        if (db == null) return;
        AddSanctionEntity(userId, actorId, action, reason, expiresAt, reportId);
        db.SaveChanges();
    }

    private void AddSanctionEntity(
        string userId, string actorId, SanctionAction action,
        string? reason, DateTimeOffset? expiresAt, string? reportId)
    {
        db?.AccountSanctions.Add(new AccountSanctionRecord
        {
            Id = $"sanction_{Guid.NewGuid():N}",
            UserId = userId,
            Action = action.ToString(),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ExpiresAt = expiresAt,
            ActorId = actorId,
            ReportId = reportId,
        });
    }
}
