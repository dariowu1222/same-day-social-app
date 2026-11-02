using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Application.Services;

/// <summary>
/// 後台登入的第二道關卡。
///
/// 一般登入（Email + 密碼）完全不受影響——那會換到一顆 App token，而 App token
/// 打不開後台。要進後台得再走一次這裡：寄一組六位數到管理者信箱，驗過之後
/// 才核發一顆獨立的、短命的、可撤銷的後台 token。
///
/// 為什麼第二因素擺在「進後台」而不是「登入」：
/// 擋的是「密碼外洩的人想進後台」，不是「使用者登入 App」。把驗證碼放在登入，
/// 全站使用者都要為了一個人的風險付出成本；放在後台門口，成本只落在該付的人身上。
///
/// 三個時間互相獨立，不要混：
///   LastSeenAt     —— 閒置多久就登出（IdleTimeout）
///   ExpiresAt      —— 這次登入最長能用多久（AbsoluteLifetime），到了一定重驗
///   LastVerifiedAt —— 最近一次輸入驗證碼的時間，封鎖／註銷要求它夠新（FreshVerification）
/// </summary>
public sealed class AdminSessionService(
    IServiceProvider services,
    IEmailSender emailSender,
    JwtService jwt,
    AdminAccessService admin,
    ILogger<AdminSessionService> logger)
{
    private readonly AppDbContext? db = services.GetService<AppDbContext>();

    /// <summary>驗證碼存活時間。比註冊碼短——這是當場要用的東西，不該留在信箱裡過夜。</summary>
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

    /// <summary>同一組碼最多試幾次。超過就作廢，重寄一組。</summary>
    private const int MaxAttempts = 5;

    /// <summary>兩次寄碼之間至少要隔這麼久。</summary>
    private static readonly TimeSpan ResendInterval = TimeSpan.FromSeconds(60);

    /// <summary>一小時內最多寄幾組。擋的是「無限重寄把猜測次數變無限」與信箱轟炸。</summary>
    private const int MaxCodesPerHour = 5;

    /// <summary>閒置多久自動登出。</summary>
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    /// <summary>一次後台登入的絕對上限，即使一直在操作也一樣。</summary>
    private static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(8);

    /// <summary>封鎖／註銷這類不可逆的動作，要求驗證碼是這段時間內通過的。</summary>
    private static readonly TimeSpan FreshVerification = TimeSpan.FromMinutes(10);

    /// <summary>JWT 自己的 exp 比 session 的絕對上限寬一點，讓失效判斷統一由 session 那張表負責。</summary>
    private static readonly TimeSpan TokenLifetime = AbsoluteLifetime + TimeSpan.FromMinutes(5);

    public enum Failure
    {
        None,
        NotAdmin,
        NoEmail,
        SmtpNotConfigured,
        SendFailed,
        CodeInvalid,
        CodeExpired,
        TooManyAttempts,
        TooManyRequests,
        Unavailable,
    }

    public sealed record ChallengeResult(Failure Failure, string? MaskedEmail);
    public sealed record VerifyResult(Failure Failure, string? Token, DateTimeOffset? ExpiresAt);

    /// <summary>
    /// 寄出進入後台的驗證碼。
    ///
    /// 呼叫端必須是「已經用 App token 登入、而且在白名單上」的人。不在白名單上
    /// 一律回 NotAdmin，由控制器翻成 404——不揭露這支端點的存在。
    /// </summary>
    public async Task<ChallengeResult> SendChallengeAsync(string userId, CodePurpose purpose, CancellationToken ct)
    {
        if (db == null)
        {
            return new ChallengeResult(Failure.Unavailable, null);
        }

        if (!admin.IsAdmin(userId))
        {
            return new ChallengeResult(Failure.NotAdmin, null);
        }

        // 驗證碼寄到帳號綁定的信箱，不是呼叫端說了算的任意地址。
        // 這是整條鏈路的錨點：控制權在信箱，不在請求內容。
        var account = await db.AuthAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDisabled, ct);
        if (account == null || string.IsNullOrWhiteSpace(account.Username))
        {
            return new ChallengeResult(Failure.NoEmail, null);
        }

        // 寄碼要節流。沒有節流的話，一顆 App token 就能無限重寄：既是信箱轟炸，
        // 也等於把「每組碼五次」變成無限次猜測——因為每封新信都重置計數。
        // 兩件事都會讓「有人在試著進後台」這個訊號消失在雜訊裡。
        var nowUtc = DateTimeOffset.UtcNow;
        var recent = await db.AdminLoginCodes.AsNoTracking()
            .Where(x => x.UserId == userId && x.CreatedAt > nowUtc.AddHours(-1))
            .Select(x => x.CreatedAt)
            .ToListAsync(ct);

        if (recent.Count >= MaxCodesPerHour || recent.Any(at => nowUtc - at < ResendInterval))
        {
            logger.LogWarning("後台驗證碼寄送被節流擋下 user={UserId} purpose={Purpose}", userId, purpose);
            return new ChallengeResult(Failure.TooManyRequests, null);
        }

        // 同用途的舊碼一律作廢：同時有兩組有效的碼，等於把猜中的機率加倍
        var purposeValue = purpose.ToString();
        await db.AdminLoginCodes
            .Where(x => x.UserId == userId && x.UsedAt == null && x.Purpose == purposeValue)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UsedAt, DateTimeOffset.UtcNow), ct);

        var code = AuthSecurity.CreateVerificationCode();
        var record = new AdminLoginCodeRecord
        {
            Id = $"alc_{Guid.NewGuid():N}",
            UserId = userId,
            Purpose = purposeValue,
            CodeHash = AuthSecurity.HashVerificationCode(code),
            ExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime),
        };
        db.AdminLoginCodes.Add(record);
        await db.SaveChangesAsync(ct);

        try
        {
            if (purpose == CodePurpose.Reverify)
            {
                await emailSender.SendAdminReverifyCodeAsync(account.Username, code, ct);
            }
            else
            {
                await emailSender.SendAdminLoginCodeAsync(account.Username, code, ct);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message == "SMTP_NOT_CONFIGURED")
        {
            record.UsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return new ChallengeResult(Failure.SmtpNotConfigured, null);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            record.UsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogError(ex, "後台驗證碼寄送失敗");
            return new ChallengeResult(Failure.SendFailed, null);
        }

        return new ChallengeResult(Failure.None, Mask(account.Username));
    }

    /// <summary>驗證碼正確就開一個新的 session，並回傳後台專用 token。</summary>
    public async Task<VerifyResult> VerifyAsync(
        string userId, string nickname, string code, string? ip, string? userAgent, CancellationToken ct)
    {
        if (db == null)
        {
            return new VerifyResult(Failure.Unavailable, null, null);
        }

        if (!admin.IsAdmin(userId))
        {
            return new VerifyResult(Failure.NotAdmin, null, null);
        }

        var (failure, record) = await ConsumeCodeAsync(userId, CodePurpose.Login, code, ct);
        if (failure != Failure.None)
        {
            return new VerifyResult(failure, null, null);
        }

        var now = record!.UsedAt!.Value;

        var session = new AdminSessionRecord
        {
            Id = $"asess_{Guid.NewGuid():N}",
            UserId = userId,
            LastSeenAt = now,
            LastVerifiedAt = now,
            ExpiresAt = now.Add(AbsoluteLifetime),
            CreatedIp = Truncate(ip, 64),
            CreatedUserAgent = Truncate(userAgent, 300),
        };
        db.AdminSessions.Add(session);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("後台登入成功 user={UserId} session={SessionId}", userId, session.Id);

        return new VerifyResult(
            Failure.None,
            jwt.IssueAdminToken(userId, nickname, session.Id, TokenLifetime),
            session.ExpiresAt);
    }

    public sealed record SessionState(bool Valid, bool RecentlyVerified, DateTimeOffset? ExpiresAt);

    /// <summary>
    /// 每一個後台請求都會走這裡：確認 session 還活著，順手把 LastSeenAt 往前推。
    ///
    /// 回傳的 RecentlyVerified 給破壞性動作用——它們要求驗證碼是最近通過的，
    /// 不能靠一個開了七小時的分頁就把人註銷掉。
    /// </summary>
    public async Task<SessionState> TouchAsync(string userId, string sessionId, CancellationToken ct)
    {
        if (db == null)
        {
            return new SessionState(false, false, null);
        }

        var session = await db.AdminSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        var now = DateTimeOffset.UtcNow;

        // userId 也要比對：token 是簽過的，但多一層比對可以讓「sid 被換掉」
        // 這種拼接式偽造在這裡就斷掉，而不是靠簽章是唯一防線。
        if (session == null
            || session.UserId != userId
            || session.RevokedAt != null
            || session.ExpiresAt <= now
            || now - session.LastSeenAt > IdleTimeout
            || !admin.IsAdmin(userId))
        {
            return new SessionState(false, false, null);
        }

        // 帳號本身的狀態也要看。ChangeStatus 現在會主動撤銷 session，但那只涵蓋
        // 「從後台按下去」這一條路徑；直接改資料庫、或日後新增的任何停權路徑
        // 都不會經過它。狀態是真相，session 只是快取。
        var accountOk = await db.Users.AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.Status == AccountStatus.ACTIVE.ToString(), ct);
        if (!accountOk)
        {
            return new SessionState(false, false, null);
        }

        var recentlyVerified = now - session.LastVerifiedAt <= FreshVerification;

        // 每次請求都寫一次有點浪費，但後台流量極低，而且閒置逾時要準就只能這樣。
        session.LastSeenAt = now;
        await db.SaveChangesAsync(ct);

        return new SessionState(true, recentlyVerified, session.ExpiresAt);
    }

    /// <summary>破壞性動作前的再驗證：碼對了就把 LastVerifiedAt 推到現在。</summary>
    public async Task<Failure> ReverifyAsync(string userId, string sessionId, string code, CancellationToken ct)
    {
        if (db == null)
        {
            return Failure.Unavailable;
        }

        var (verified, _) = await ConsumeCodeAsync(userId, CodePurpose.Reverify, code, ct);
        if (verified != Failure.None)
        {
            return verified;
        }

        var session = await db.AdminSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session == null || session.UserId != userId || session.RevokedAt != null)
        {
            return Failure.CodeInvalid;
        }

        session.LastVerifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Failure.None;
    }

    /// <summary>登出：把這次 session 標成撤銷，token 當場失效。</summary>
    public async Task RevokeAsync(string sessionId, CancellationToken ct)
    {
        if (db == null) return;
        await db.AdminSessions
            .Where(x => x.Id == sessionId && x.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), ct);
    }

    /// <summary>
    /// 撤銷指定使用者所有還活著的後台 session。
    ///
    /// 自己用（手機掉了、懷疑外洩），也給另一個管理者用——出事時最需要的動作
    /// 就是「把那個人現在的連線切掉」，而這件事不該要等重新部署才做得到。
    /// </summary>
    public async Task RevokeAllAsync(string userId, CancellationToken ct)
    {
        if (db == null) return;
        await db.AdminSessions
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), ct);
    }

    /// <summary>
    /// 驗一組碼並消耗掉它。登入與再驗證共用這一段，但 purpose 必須相符——
    /// 登入收到的碼不能拿去通過「封鎖使用者」的再驗證，反之亦然。
    /// 兩者的授權份量差很多，信裡也寫了各自的用途，不該互換。
    ///
    /// 失敗計數用 ExecuteUpdate 做成 attempts = attempts + 1 的原子遞增。
    /// 先讀再寫的話，同時送進來的請求會全部讀到同一個舊值、全部寫回同一個新值，
    /// 十次猜測只記一次——「五次上限」就形同不存在，而且那是唯一會把碼燒掉、
    /// 唯一會留下帶內警訊的路徑。
    /// </summary>
    private async Task<(Failure Failure, AdminLoginCodeRecord? Record)> ConsumeCodeAsync(
        string userId, CodePurpose purpose, string? code, CancellationToken ct)
    {
        var purposeValue = purpose.ToString();
        var record = await db!.AdminLoginCodes
            .Where(x => x.UserId == userId && x.UsedAt == null && x.Purpose == purposeValue)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (record == null) return (Failure.CodeInvalid, null);
        if (record.ExpiresAt <= DateTimeOffset.UtcNow) return (Failure.CodeExpired, null);

        // 先原子地佔一次嘗試額度。回 0 列代表已達上限或碼已被用掉。
        var claimed = await db.AdminLoginCodes
            .Where(x => x.Id == record.Id && x.UsedAt == null && x.Attempts < MaxAttempts)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Attempts, x => x.Attempts + 1), ct);

        if (claimed == 0)
        {
            await db.AdminLoginCodes
                .Where(x => x.Id == record.Id && x.UsedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.UsedAt, DateTimeOffset.UtcNow), ct);
            logger.LogWarning("後台驗證碼嘗試次數用盡 user={UserId} purpose={Purpose}", userId, purposeValue);
            return (Failure.TooManyAttempts, null);
        }

        if (!AuthSecurity.VerifyVerificationCode(code ?? "", record.CodeHash))
        {
            return (Failure.CodeInvalid, null);
        }

        var now = DateTimeOffset.UtcNow;
        var consumed = await db.AdminLoginCodes
            .Where(x => x.Id == record.Id && x.UsedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UsedAt, now), ct);

        // 0 列代表同一組碼被另一個請求搶先用掉了，不能讓兩邊都算通過
        if (consumed == 0) return (Failure.CodeInvalid, null);

        record.UsedAt = now;
        return (Failure.None, record);
    }

    public enum CodePurpose
    {
        /// <summary>進入後台。</summary>
        Login,
        /// <summary>執行不可逆的處置前再確認一次。</summary>
        Reverify,
    }

    /// <summary>只露出足夠讓本人認出是哪個信箱的程度，不要在回應裡把完整地址送回去。</summary>
    private static string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        var name = email[..at];
        var domain = email[at..];
        var head = name.Length <= 2 ? name[..1] : name[..2];
        return $"{head}{new string('*', Math.Max(3, name.Length - head.Length))}{domain}";
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null : (value.Length <= max ? value : value[..max]);
}
