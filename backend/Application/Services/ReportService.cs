using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Application.Services;

public sealed class ReportService
{
    private readonly AppDbContext? db;
    private readonly NotificationService? notifications;
    private readonly ILogger<ReportService>? logger;

    public ReportService(IServiceProvider services)
    {
        db = services.GetService<AppDbContext>();
        notifications = services.GetService<NotificationService>();
        logger = services.GetService<ILogger<ReportService>>();
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

        // 同一人對同一標的只記一次（DB 也有唯一索引把關）
        var existing = db.Reports.AsNoTracking().FirstOrDefault(x =>
            x.TargetType == targetType.ToString() &&
            x.TargetId == targetId &&
            x.ReporterId == reporterId);
        if (existing != null)
        {
            return new SubmitResult(existing.ToDomain(), true, null);
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

        db.Reports.Add(report.ToRecord());
        db.SaveChanges();

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
    public Report? Review(
        string reportId,
        string actorId,
        ReportStatus status,
        string? resolution,
        bool? hideTarget)
    {
        if (db == null) return null;

        var record = db.Reports.FirstOrDefault(x => x.Id == reportId);
        if (record == null) return null;

        record.Status = status.ToString();
        record.ReviewedBy = actorId;
        record.ReviewedAt = DateTimeOffset.UtcNow;
        record.Resolution = string.IsNullOrWhiteSpace(resolution) ? null : resolution.Trim();

        if (hideTarget.HasValue)
        {
            SetTargetHidden(record.TargetType, record.TargetId, hideTarget.Value);
        }

        db.SaveChanges();

        LogAudit(actorId, $"REVIEW_{status}", reportId,
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
        if (post != null) post.IsHidden = hidden;
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
