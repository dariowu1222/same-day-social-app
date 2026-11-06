using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Microsoft.Extensions.Logging;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Application.Services;

public sealed class ReportService
{
    private readonly AppDbContext? db;
    private readonly NotificationService? notifications;
    private readonly ILogger<ReportService>? logger;
    private readonly IEmailSender? emailSender;

    public ReportService(IServiceProvider services)
    {
        db = services.GetService<AppDbContext>();
        notifications = services.GetService<NotificationService>();
        logger = services.GetService<ILogger<ReportService>>();
        emailSender = services.GetService<IEmailSender>();
    }

    /// <summary>
    /// 分類對應的嚴重度，決定審核佇列的先後。
    /// SELF_HARM 最高：那是有人可能正在受傷，不是內容違規。
    /// </summary>
    private static int SeverityFor(ReportCategory category) => category switch
    {
        ReportCategory.SELF_HARM => 100,
        ReportCategory.VIOLENCE => 80,
        ReportCategory.SEXUAL => 70,
        ReportCategory.HARASSMENT => 60,
        ReportCategory.HATE => 60,
        ReportCategory.IMPERSONATION => 40,
        ReportCategory.SCAM => 40,
        _ => 20,
    };

    // 自動暫時隱藏的權重門檻。
    // 舊制「不分理由滿 3 次就隱藏」只要三個帳號串通就能讓任何貼文消失；
    // 改用權重後，輕微指控要更多人附議，嚴重指控則能較快擋下。
    private const int AutoHideThreshold = 150;

    public sealed record SubmitResult(Report? Report, bool AlreadyReported, string? Error);

    public SubmitResult Submit(
        string reporterId,
        ReportTargetType targetType,
        string targetId,
        ReportCategory category,
        string? detail)
    {
        if (string.IsNullOrWhiteSpace(reporterId) || string.IsNullOrWhiteSpace(targetId))
        {
            return new SubmitResult(null, false, "REPORT_INVALID");
        }

        if (db == null)
        {
            return new SubmitResult(null, false, "REPORT_UNAVAILABLE");
        }

        var target = ResolveTarget(targetType, targetId);
        if (target == null)
        {
            return new SubmitResult(null, false, "REPORT_TARGET_NOT_FOUND");
        }

        // 檢舉自己沒有意義，多半是誤觸
        if (target.Value.OwnerId == reporterId)
        {
            return new SubmitResult(null, false, "REPORT_SELF");
        }

        // 同一人對同一標的只記一次
        var existing = FindExisting(reporterId, targetType, targetId);
        if (existing != null)
        {
            return new SubmitResult(existing, true, null);
        }

        var report = new Report
        {
            Id = $"report_{Guid.NewGuid():N}",
            TargetType = targetType,
            TargetId = targetId,
            ReporterId = reporterId,
            ReportedUserId = target.Value.OwnerId,
            Category = category,
            Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(),
            Status = ReportStatus.PENDING,
            Severity = SeverityFor(category),
            // 內容之後可能被刪改，當下就留存快照，審核時才有東西可看
            ContextSnapshot = Truncate(target.Value.Snapshot, 2000),
        };

        var record = report.ToRecord();
        db.Reports.Add(record);

        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateException ex) when (IsDuplicateReport(ex))
        {
            // 上面的「有沒有送過」是先讀再寫，兩發同時進來會雙雙讀到沒有、雙雙寫入，
            // 由唯一索引擋下第二發。這不是錯誤，是「已經檢舉過了」——使用者按兩下
            // 送出鈕、或手機上連點，都會走到這裡，不該回 500。
            db.Entry(record).State = EntityState.Detached;
            var saved = FindExisting(reporterId, targetType, targetId);
            if (saved != null)
            {
                return new SubmitResult(saved, true, null);
            }
            throw;
        }

        if (category == ReportCategory.SELF_HARM)
        {
            NotifySelfHarmConcern(target.Value.OwnerId);
        }
        else
        {
            ApplyAutoModeration(targetType, targetId);
        }

        return new SubmitResult(report, false, null);
    }

    private Report? FindExisting(string reporterId, ReportTargetType targetType, string targetId)
    {
        var record = db!.Reports.AsNoTracking().FirstOrDefault(x =>
            x.TargetType == targetType.ToString() &&
            x.TargetId == targetId &&
            x.ReporterId == reporterId);
        return record?.ToDomain();
    }

    /// <summary>唯一索引 ux_reports_target_reporter 擋下的重複檢舉。</summary>
    private static bool IsDuplicateReport(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>
    /// 依權重總和決定是否暫時隱藏。SELF_HARM 不計入——
    /// 把可能在求救的人的貼文藏起來，只會讓他更沒有出口。
    /// </summary>
    private void ApplyAutoModeration(ReportTargetType targetType, string targetId)
    {
        if (db == null || targetType != ReportTargetType.RANT_POST) return;

        var selfHarm = ReportCategory.SELF_HARM.ToString();
        var pending = db.Reports.AsNoTracking()
            .Where(x => x.TargetType == targetType.ToString()
                        && x.TargetId == targetId
                        && x.Category != selfHarm
                        && x.Status != ReportStatus.DISMISSED.ToString())
            .Select(x => x.Severity)
            .ToList();

        if (pending.Sum() < AutoHideThreshold) return;

        var post = db.RantPosts.FirstOrDefault(x => x.Id == targetId);
        if (post == null || post.IsHidden) return;
        post.IsHidden = true;
        db.SaveChanges();
    }

    /// <summary>
    /// 有人替當事人送出自我傷害的檢舉時，發一則關懷通知。
    /// 刻意不提「有人檢舉你」——那會讓當事人覺得被監視、被舉發，反而更退縮。
    /// </summary>
    private void NotifySelfHarmConcern(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        notifications?.Add(
            userId,
            "SELF_HARM_SUPPORT",
            "想讓你知道，你並不孤單 🤍",
            "如果最近很不好受，願意的話可以找人聊聊。安心專線 1925、生命線 1995，24 小時都有人在。",
            "support",
            null);
    }

    private readonly record struct TargetInfo(string OwnerId, string Snapshot);

    /// <summary>取出被檢舉標的的作者與內容快照。找不到回 null。</summary>
    private TargetInfo? ResolveTarget(ReportTargetType targetType, string targetId)
    {
        if (db == null) return null;

        switch (targetType)
        {
            case ReportTargetType.RANT_POST:
                var post = db.RantPosts.AsNoTracking().FirstOrDefault(x => x.Id == targetId);
                return post == null ? null : new TargetInfo(post.UserId, post.Content);

            case ReportTargetType.RANT_REPLY:
                var reply = db.RantReplies.AsNoTracking().FirstOrDefault(x => x.Id == targetId);
                return reply == null ? null : new TargetInfo(reply.UserId, reply.Content);

            case ReportTargetType.CHAT_MESSAGE:
                var message = db.ChatMessages.AsNoTracking().FirstOrDefault(x => x.Id == targetId);
                return message == null ? null : new TargetInfo(message.SenderId, message.Content);

            case ReportTargetType.USER:
                var user = db.Users.AsNoTracking().FirstOrDefault(x => x.Id == targetId);
                return user == null ? null : new TargetInfo(user.Id, $"暱稱：{user.Nickname}");

            default:
                return null;
        }
    }

    // ── 審核端 ─────────────────────────────────────────

    /// <summary>
    /// 待審佇列。預設只看未處理的，依嚴重度、再依時間排序，
    /// 讓自我傷害這類最緊急的永遠排在最前面。
    /// </summary>
    public List<Report> GetQueue(ReportStatus? status, int limit = 50)
    {
        if (db == null) return [];
        limit = Math.Clamp(limit, 1, 200);

        var query = db.Reports.AsNoTracking().AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value.ToString());
        }

        return query
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.CreatedAt)
            .Take(limit)
            .ToList()
            .Select(x => x.ToDomain())
            .ToList();
    }

    /// <summary>
    /// 這批檢舉指向的樹洞貼文現在是不是隱藏中。
    ///
    /// 後台原本看不到這件事：佇列只顯示檢舉本身，審核的人無從判斷那篇到底還在不在。
    /// 一次查完整批，不要每一列各問一次（那張清單已經有一個 N+1 了，見 CountAgainstUser）。
    /// </summary>
    public Dictionary<string, bool> HiddenRantPosts(IEnumerable<string> postIds)
    {
        if (db == null) return [];

        var ids = postIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        if (ids.Count == 0) return [];

        return db.RantPosts.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.IsHidden })
            .ToDictionary(x => x.Id, x => x.IsHidden);
    }

    /// <summary>同一被檢舉人累積的未駁回檢舉數，供審核時判斷是否累犯。</summary>
    public int CountAgainstUser(string userId)
    {
        if (db == null || string.IsNullOrWhiteSpace(userId)) return 0;
        var dismissed = ReportStatus.DISMISSED.ToString();
        return db.Reports.AsNoTracking()
            .Count(x => x.ReportedUserId == userId && x.Status != dismissed);
    }

    /// <summary>
    /// 審核處置。hideTarget: true 隱藏內容、false 解除隱藏、null 不更動內容。
    /// 每次處置都寫入軌跡，事後可稽核誰做了什麼。
    /// </summary>
    public async Task<Report?> ReviewAsync(
        string reportId,
        string actorId,
        ReportStatus status,
        string? resolution,
        bool? hideTarget,
        CancellationToken cancellationToken = default)
    {
        if (db == null) return null;

        var record = db.Reports.FirstOrDefault(x => x.Id == reportId);
        if (record == null) return null;

        // 先記下原本的狀態。下面那則通知只能在「這一次才變成成立」時發，而讀完
        // 就覆蓋的話，PENDING→RESOLVED 跟 RESOLVED→RESOLVED 在後面看起來一模一樣。
        // 後台的結案按鈕在送出期間沒有 disabled，連點兩下就是兩個請求；「已處理」
        // 清單裡也還給得出「處理這筆」。NotificationService.Add 沒有去重，
        // 所以少了這一行，檢舉人會為同一筆檢舉收到兩則一樣的通知。
        var wasResolved = record.Status == ReportStatus.RESOLVED.ToString();
        var previousStatus = record.Status;

        record.Status = status.ToString();
        record.ReviewedBy = actorId;
        record.ReviewedAt = DateTimeOffset.UtcNow;
        record.Resolution = string.IsNullOrWhiteSpace(resolution) ? null : resolution.Trim();

        if (hideTarget.HasValue)
        {
            SetTargetHidden(record.TargetType, record.TargetId, hideTarget.Value);

            // 退回給作者時，連同這篇上面其他還在計分的檢舉一起駁回。
            //
            // 不做的話退回根本站不住：ApplyAutoModeration 每次有新檢舉進來，
            // 都會把這篇「非 DISMISSED」的檢舉權重重新加總一次跟門檻比
            // ——而「結案」是 RESOLVED，仍然計入。所以舊的那幾筆還在累計，
            // 下一筆檢舉一進來就立刻又超過門檻，貼文又不見了。
            //
            // 判定內容沒問題，本來就等於針對這篇的那些檢舉都不成立。
            if (!hideTarget.Value)
            {
                DismissOtherReportsOn(record.TargetType, record.TargetId, exceptReportId: record.Id);
            }
        }

        db.SaveChanges();

        // 駁回＝這筆檢舉不成立，那「東西還是不見」就沒有道理。
        //
        // 放在 SaveChanges 之後才算得準：上面那行 record.Status 只改了記憶體裡的
        // 追蹤物件，資料庫裡還是舊狀態，這時候加總會把剛駁回的這一筆也算進去。
        //
        // 只有在扣掉之後真的低於門檻才放出來。一篇被三個人檢舉的貼文，
        // 管理者判定其中一筆是亂檢舉，不該讓另外兩筆成立的檢舉一起失效——
        // 那種情況要退回，用「退回給作者」那顆（它會把其餘的一併駁回）。
        var restored = hideTarget == false;
        if (status == ReportStatus.DISMISSED
            && !hideTarget.HasValue
            && RestoreIfBelowThreshold(record.TargetType, record.TargetId))
        {
            restored = true;
        }

        // 實際結果決定最終狀態。內容真的被放回牆上就記成「已退回」而不只是
        // 「已駁回」——那是一筆誤判的紀錄，後台要能單獨翻出來看。
        // 「成立」不會變成退回：那是相反的判斷，就算手動解除隱藏也一樣。
        var finalStatus = restored && status == ReportStatus.DISMISSED
            ? ReportStatus.RESTORED
            : status;
        record.Status = finalStatus.ToString();
        db.SaveChanges();

        // 檢舉成立才通知檢舉人。這是整條流程對他唯一的回音——沒有它，
        // 他不會知道自己的檢舉有沒有被看過，而聊天室也會在他毫無預期時變成唯讀。
        // 駁回不通知：那多半只會變成爭論的起點，而且「沒有處置」本身就是結果。
        //
        // SELF_HARM 也不通知，而且那種單子不會讓對話變成唯讀
        // （見 ChatService.GetFrozenRoomIds）。替朋友求救的人不該收到一則
        // 「已對該帳號做出處置」——那不是他按下去的意思。
        if (status == ReportStatus.RESOLVED
            && !wasResolved
            && record.TargetType == ReportTargetType.USER.ToString()
            && record.Category != ReportCategory.SELF_HARM.ToString()
            && !string.IsNullOrWhiteSpace(record.ReporterId))
        {
            notifications?.Add(
                record.ReporterId,
                "REPORT",
                "你的檢舉已處理",
                "我們確認了你的檢舉並對該帳號做出處置。與對方的對話已經關閉，你可以隨時退出。",
                "chat");
        }

        // 寄信給檢舉人與被檢舉人。只在狀態真的改變時寄，而且 SELF_HARM 一封都不寄。
        // 結果落地到 outcome_mail_sent：寄信會失敗（SMTP 額度、查無信箱），
        // 而失敗之後沒有地方看得出來，管理者會以為當事人已經被通知了。
        if (previousStatus != finalStatus.ToString() && finalStatus != ReportStatus.PENDING)
        {
            var stillHidden = IsTargetHidden(record.TargetType, record.TargetId);
            record.OutcomeMailSent = await SendOutcomeEmailsAsync(
                record, finalStatus == ReportStatus.RESOLVED, stillHidden, cancellationToken);
            db.SaveChanges();
        }

        LogAudit(actorId, $"REVIEW_{finalStatus}", reportId,
            hideTarget switch
            {
                true => "並隱藏內容",
                false => "並解除隱藏",
                _ => null,
            });

        return record.ToDomain();
    }

    /// <summary>目前只有樹洞貼文支援隱藏；其他型別的處置留待後續擴充。</summary>
    private void SetTargetHidden(string targetType, string targetId, bool hidden)
    {
        if (db == null) return;
        if (targetType != ReportTargetType.RANT_POST.ToString()) return;

        var post = db.RantPosts.FirstOrDefault(x => x.Id == targetId);
        if (post == null) return;

        post.IsHidden = hidden;

        // 退回給原作者時，把檢舉計數一起歸零。
        //
        // 自動隱藏的門檻是「三個不同的人檢舉過」（RantService.Report），而那個計數
        // 不會因為後台判定不成立就減少。不歸零的話，貼文退回去之後只要再多一個人
        // 檢舉就立刻又被藏起來——對原作者來說就是「它又不見了」，而且沒有人動過後台。
        // 既有的 rant_reports 列留著，稽核軌跡不受影響。
        if (!hidden) post.ReportCount = 0;
    }

    /// <summary>這則檢舉的標的現在是不是隱藏中。非樹洞貼文一律 false（還不支援隱藏）。</summary>
    private bool IsTargetHidden(string targetType, string targetId)
    {
        if (db == null || targetType != ReportTargetType.RANT_POST.ToString()) return false;
        return db.RantPosts.AsNoTracking().Any(x => x.Id == targetId && x.IsHidden);
    }

    /// <summary>
    /// 把處理結果寄給檢舉人與被檢舉人。
    ///
    /// 為什麼兩邊都寄：檢舉的人需要知道自己的檢舉有沒有被看過（不然下次就不檢舉了），
    /// 被檢舉的人需要知道自己的東西為什麼不見了——「哪天發現東西不見卻不知道
    /// 發生過什麼事」是最糟的體驗，而且那正是會讓人離開的那一種。
    ///
    /// 三件事刻意不做：
    ///  1. SELF_HARM 一封都不寄。那是替人求救，不是舉發違規；寄一封
    ///     「你被檢舉了」給一個可能正在受傷的人，傷害遠大於資訊價值。
    ///     （站內已經有一則關懷通知，見 NotifySelfHarmConcern。）
    ///  2. 不寄處理說明（resolution）。那是後台內部的備註欄，寫的時候沒有
    ///     預期會被當事人看到。
    ///  3. 兩封信都不提對方是誰。對被檢舉人尤其重要——那是報復的起點。
    ///
    /// 寄信失敗不能讓審核失敗：信寄不出去頂多是當事人沒收到通知，
    /// 而處置本身已經生效了。失敗各自吞掉並留 log。
    /// </summary>
    private async Task<bool?> SendOutcomeEmailsAsync(
        ReportRecord record, bool upheld, bool contentHidden, CancellationToken cancellationToken)
    {
        if (db == null || emailSender == null) return null;
        if (record.Category == ReportCategory.SELF_HARM.ToString()) return null;

        var recipients = new[] { record.ReporterId, record.ReportedUserId }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        if (recipients.Count == 0) return null;

        var emails = db.AuthAccounts.AsNoTracking()
            .Where(x => recipients.Contains(x.UserId))
            .Select(x => new { x.UserId, x.Username })
            .ToDictionary(x => x.UserId, x => x.Username);

        var attempted = 0;
        var failed = 0;

        await SendOneAsync(record.ReporterId, "檢舉人", email =>
            emailSender.SendReportOutcomeToReporterAsync(email, upheld, cancellationToken));

        await SendOneAsync(record.ReportedUserId, "被檢舉人", email =>
            emailSender.SendReportOutcomeToReportedAsync(email, upheld, contentHidden, cancellationToken));

        // 一封都沒寄（兩邊都查不到信箱）回 null，不要謊報成功
        if (attempted == 0) return null;
        return failed == 0;

        async Task SendOneAsync(string? userId, string who, Func<string, Task> send)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;
            if (!emails.TryGetValue(userId, out var email) || string.IsNullOrWhiteSpace(email)) return;

            attempted++;
            try
            {
                await send(email);
            }
            catch (Exception ex)
            {
                failed++;
                logger?.LogWarning(ex, "檢舉處理結果寄信失敗（{Who}） reportId={ReportId}", who, record.Id);
            }
        }
    }

    /// <summary>
    /// 這篇現在還有沒有足夠的檢舉撐住隱藏狀態；沒有就放回牆上。有動到回 true。
    ///
    /// 隱藏是 ApplyAutoModeration 依「非 DISMISSED 的檢舉權重總和」算出來的，
    /// 那是一個會變的量——駁回一筆就該重算一次，否則判定不成立之後東西還是不見。
    /// 呼叫前請先 SaveChanges，這裡是直接對資料庫加總。
    /// </summary>
    private bool RestoreIfBelowThreshold(string targetType, string targetId)
    {
        if (db == null || targetType != ReportTargetType.RANT_POST.ToString()) return false;

        var post = db.RantPosts.FirstOrDefault(x => x.Id == targetId);
        if (post == null || !post.IsHidden) return false;

        var selfHarm = ReportCategory.SELF_HARM.ToString();
        var dismissed = ReportStatus.DISMISSED.ToString();
        var weight = db.Reports.AsNoTracking()
            .Where(x => x.TargetType == targetType
                        && x.TargetId == targetId
                        && x.Category != selfHarm
                        && x.Status != dismissed)
            .Sum(x => (int?)x.Severity) ?? 0;

        if (weight >= AutoHideThreshold) return false;

        post.IsHidden = false;
        post.ReportCount = 0;
        return true;
    }

    /// <summary>
    /// 把這篇上面其他還在計分的檢舉一併駁回，好讓退回之後的貼文站得住。
    ///
    /// 跳過兩種：這次正在處理的那一筆（它的狀態由管理者自己決定，不要覆蓋掉），
    /// 以及 SELF_HARM——那本來就不計入自動隱藏的權重（見 ApplyAutoModeration），
    /// 而且它是一張關懷單，不是內容違規的指控，不該被順手判定為不成立。
    /// </summary>
    private void DismissOtherReportsOn(string targetType, string targetId, string exceptReportId)
    {
        if (db == null) return;

        var selfHarm = ReportCategory.SELF_HARM.ToString();
        var dismissed = ReportStatus.DISMISSED.ToString();

        var others = db.Reports
            .Where(x => x.TargetType == targetType
                        && x.TargetId == targetId
                        && x.Id != exceptReportId
                        && x.Category != selfHarm
                        && x.Status != dismissed)
            .ToList();

        foreach (var other in others)
        {
            other.Status = dismissed;
        }
    }

    /// <summary>
    /// 寫入審核軌跡。後台能讀到私訊與貼文內容，
    /// 因此連「查看佇列」都要留痕，而不是只記處置。
    /// </summary>
    public void LogAudit(string actorId, string action, string? reportId, string? note)
    {
        if (db == null) return;

        try
        {
            db.ModerationAudits.Add(new ModerationAuditRecord
            {
                Id = $"audit_{Guid.NewGuid():N}",
                ActorId = actorId,
                Action = action,
                ReportId = reportId,
                Note = note,
            });
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            // 軌跡是輔助功能，寫不進去不該讓後台整個不能用
            //（例如只是查看會員列表，也跟著 500）。
            // 但不能靜默吞掉：寫進應用日誌，維運才看得到軌跡有缺口。
            logger?.LogError(ex, "審核軌跡寫入失敗 actor={ActorId} action={Action}", actorId, action);

            // 把寫失敗的項目移出追蹤，否則同一個請求裡之後的 SaveChanges 都會被它拖著一起失敗
            foreach (var entry in db.ChangeTracker.Entries<ModerationAuditRecord>().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= max ? value : value[..max] + "…";
    }
}
