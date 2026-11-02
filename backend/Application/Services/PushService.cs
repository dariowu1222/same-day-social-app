using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Application.Services;

/// <summary>一次彙總推播的執行結果。排程把它印在 log 裡，是這條鏈路唯一的可觀測性。</summary>
public sealed record PushDigestResult(
    string Reason, int Scanned, int Pushed, int Failed, int Skipped, int TokensRemoved);

/// <summary>
/// 每兩小時一次的彙總推播。
///
/// 刻意不做「每則訊息即時推」：那會把使用者轟炸到關掉通知。這裡每兩小時
/// 把未讀訊息與未讀通知合成一則，而且只在「距離上次推播之後真的有新東西」
/// 時才送——否則每兩小時收到同一句話，第一天就會被關掉。
///
/// 觸發源放外部（GitHub Actions）而不是 BackgroundService：Render 免費方案
/// 閒置 15 分鐘就休眠、行程整個停掉，PeriodicTimer 不會跑、醒來還會重新計時。
/// </summary>
public sealed class PushService(
    IServiceProvider services,
    PushSender sender,
    IOptions<PushOptions> options,
    ILogger<PushService> logger)
{
    private readonly AppDbContext? db = services.GetService<AppDbContext>();
    private readonly PushOptions options = options.Value;

    private static readonly TimeZoneInfo Taipei = ResolveTaipei();

    private static TimeZoneInfo ResolveTaipei()
    {
        // Linux（Render）用 IANA 名稱、Windows 用自己的 id，兩邊都試
        foreach (var id in new[] { "Asia/Taipei", "Taipei Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        // 台灣固定 UTC+8、沒有日光節約，找不到時區資料時直接寫死也不會錯
        return TimeZoneInfo.CreateCustomTimeZone("TW", TimeSpan.FromHours(8), "Taipei", "Taipei");
    }

    /// <summary>把一台裝置的 token 綁到使用者身上。同一個 token 換人登入時改綁。</summary>
    public void RegisterToken(string userId, string token, string platform)
    {
        if (db == null || string.IsNullOrWhiteSpace(token)) return;

        var existing = db.DeviceTokens.FirstOrDefault(x => x.Token == token);
        if (existing != null)
        {
            existing.UserId = userId;
            existing.Platform = platform;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.DeviceTokens.Add(new DeviceTokenRecord
            {
                Id = $"dev_{Guid.NewGuid():N}",
                UserId = userId,
                Token = token,
                Platform = platform,
            });
        }

        db.SaveChanges();
    }

    /// <summary>登出時解除綁定。只刪這個使用者自己的那一筆，不碰別人的裝置。</summary>
    public void UnregisterToken(string userId, string token)
    {
        if (db == null || string.IsNullOrWhiteSpace(token)) return;
        db.DeviceTokens.Where(x => x.Token == token && x.UserId == userId).ExecuteDelete();
    }

    public async Task<PushDigestResult> RunDigestAsync(CancellationToken ct)
    {
        if (db == null || !sender.IsConfigured)
        {
            return new PushDigestResult("NOT_CONFIGURED", 0, 0, 0, 0, 0);
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var hour = TimeZoneInfo.ConvertTime(nowUtc, Taipei).Hour;

        // 安靜時段。cron 本來就排在白天，這是擋手動觸發用的第二道防線。
        var quiet = options.QuietStartHour > options.QuietEndHour
            ? hour >= options.QuietStartHour || hour < options.QuietEndHour
            : hour >= options.QuietStartHour && hour < options.QuietEndHour;
        if (quiet)
        {
            return new PushDigestResult("QUIET_HOURS", 0, 0, 0, 0, 0);
        }

        var lookback = nowUtc.AddDays(-options.LookbackDays);

        // 未讀訊息：水位是 COALESCE(last_read_at, joined_at)，自己發的與已收回的不算。
        // 兩句單純的查詢再在記憶體裡配對，不寫成 join + group by——後者要賭 EF 的
        // 關聯聚合翻譯，翻不出來會退化成客戶端評估把整張表拉進來。
        // 這是每兩小時跑一次的背景作業，lookback 也限制了資料量，記憶體配對很划算。
        // 順序很重要：先取「這段期間真的有訊息的房間」，再只撈那些房的成員。
        // 反過來寫的話，每次執行都會把整張 chat_room_users 拉進記憶體，
        // 成本隨歷史單調成長，而真正用得到的通常只佔一小部分。
        var recentMessages = db.ChatMessages.AsNoTracking()
            .Where(m => !m.IsRecalled && m.CreatedAt > lookback)
            .Select(m => new { m.ChatRoomId, m.SenderId, m.CreatedAt })
            .ToList();

        if (recentMessages.Count == 0 && db.Notifications.AsNoTracking().All(n => n.IsRead || n.CreatedAt <= lookback))
        {
            return new PushDigestResult("NOTHING_TO_SEND", 0, 0, 0, 0, 0);
        }

        var activeRoomIds = recentMessages.Select(m => m.ChatRoomId).Distinct().ToList();

        var memberships = db.ChatRoomUsers.AsNoTracking()
            .Where(u => activeRoomIds.Contains(u.ChatRoomId))
            .Select(u => new { u.ChatRoomId, u.UserId, At = u.LastReadAt ?? u.JoinedAt })
            .ToList()
            .GroupBy(x => x.ChatRoomId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 檢舉成立之後那間房就唯讀了，回不了訊息。既然回不了，就不該再為
        // 那個人的新訊息推播——被處置的人還傳得進來（他不會知道對方檢舉過他），
        // 推播會變成繞過唯讀的另一條騷擾管道。
        // 跟著唯讀的條件走：只看審核成立（RESOLVED）的檢舉。還在排隊審的不算，
        // 否則一封誤報就能讓對方的訊息從此不再通知，等於未審先判。
        // 整張表一次撈：USER 類型的檢舉在小規模下只有幾列，比每人各查一次便宜。
        var userTarget = ReportTargetType.USER.ToString();
        var resolved = ReportStatus.RESOLVED.ToString();
        var reportedPairs = db.Reports.AsNoTracking()
            .Where(r => r.TargetType == userTarget && r.Status == resolved)
            .Select(r => new { r.ReporterId, r.ReportedUserId })
            .ToList()
            .Select(r => (Reporter: r.ReporterId, Reported: r.ReportedUserId))
            .ToHashSet();

        var perUser = new Dictionary<string, MessageAccumulator>();
        foreach (var message in recentMessages)
        {
            if (!memberships.TryGetValue(message.ChatRoomId, out var members)) continue;

            foreach (var member in members)
            {
                if (member.UserId == message.SenderId) continue;
                if (message.CreatedAt <= member.At) continue;
                if (reportedPairs.Contains((member.UserId, message.SenderId))) continue;

                if (!perUser.TryGetValue(member.UserId, out var acc))
                {
                    acc = new MessageAccumulator();
                    perUser[member.UserId] = acc;
                }

                acc.Count++;
                acc.Rooms.Add(message.ChatRoomId);
                if (message.CreatedAt > acc.Latest) acc.Latest = message.CreatedAt;
            }
        }

        var messageStats = perUser.ToDictionary(
            x => x.Key,
            x => new
            {
                UserId = x.Key,
                x.Value.Count,
                x.Value.Latest,
                Rooms = x.Value.Rooms.Count,
                AnyRoomId = x.Value.Rooms.First(),
            });

        // 未讀通知：按類型分組，因為每種類型對應不同的開關
        var notificationStats = db.Notifications.AsNoTracking()
            .Where(n => !n.IsRead && n.CreatedAt > lookback)
            .GroupBy(n => new { n.RecipientId, n.Type })
            .Select(g => new
            {
                g.Key.RecipientId,
                g.Key.Type,
                Count = g.Count(),
                Latest = g.Max(x => x.CreatedAt),
            })
            .ToList();

        var candidateIds = messageStats.Keys
            .Union(notificationStats.Select(x => x.RecipientId))
            .ToList();

        if (candidateIds.Count == 0)
        {
            return new PushDigestResult("NOTHING_TO_SEND", 0, 0, 0, 0, 0);
        }

        var settings = db.UserSettings.AsNoTracking()
            .Where(x => candidateIds.Contains(x.UserId))
            .ToDictionary(x => x.UserId);

        var states = db.PushDigestStates
            .Where(x => candidateIds.Contains(x.UserId))
            .ToDictionary(x => x.UserId);

        var tokensByUser = db.DeviceTokens.AsNoTracking()
            .Where(x => candidateIds.Contains(x.UserId))
            .ToList()
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Token).Distinct().ToList());

        var floor = nowUtc.AddMinutes(-options.MinIntervalMinutes);

        // 先過完所有閘門，再截斷。順序反過來的話，已經推過、沒有新東西的人
        // 照樣佔掉名額，排在後面的人就永遠輪不到。
        var plans = new List<DigestPlan>();
        foreach (var userId in candidateIds)
        {
            if (!tokensByUser.TryGetValue(userId, out var tokens) || tokens.Count == 0) continue;

            var setting = settings.GetValueOrDefault(userId);
            var plan = BuildPlan(userId, setting, messageStats.GetValueOrDefault(userId) is { } m
                    ? new MessageSummary(m.Count, m.Rooms, m.Latest, m.AnyRoomId)
                    : null,
                notificationStats.Where(x => x.RecipientId == userId)
                    .Select(x => new NotificationSummary(x.Type, x.Count, x.Latest))
                    .ToList());

            if (plan == null) continue;

            var state = states.GetValueOrDefault(userId);
            // 上次推播之後沒有新東西 → 不要重複推同一句話
            if (state?.LastEventAt != null && plan.EventAt <= state.LastEventAt) continue;
            // 頻率地板：排程可能早到或補跑，不要兩小時內推第二次
            if (state?.LastPushedAt != null && state.LastPushedAt > floor) continue;

            plans.Add(plan with { Tokens = tokens, LastPushedAt = state?.LastPushedAt });
        }

        var scanned = plans.Count;

        // 最久沒收到推播的優先，避免同一批人永遠佔住名額
        var batch = plans
            .OrderBy(x => x.LastPushedAt ?? DateTimeOffset.MinValue)
            .Take(options.MaxUsersPerRun)
            .ToList();

        var pushed = 0;
        var failed = 0;
        var deadTokens = new List<string>();

        foreach (var plan in batch)
        {
            ct.ThrowIfCancellationRequested();

            var result = await sender.SendAsync(plan.Tokens, plan.Title, plan.Body, plan.RoomId, ct);
            deadTokens.AddRange(result.DeadTokens);

            if (result.SuccessCount == 0)
            {
                // 一則都沒送出去就不要動水位。先前的寫法是不管成敗都推進，
                // 結果 FCM 一故障，那批未讀永遠不會再被推播，而排程還顯示成功。
                failed++;
                continue;
            }

            UpsertState(plan.UserId, plan.EventAt, nowUtc);
            pushed++;
        }

        if (deadTokens.Count > 0)
        {
            db.DeviceTokens.Where(x => deadTokens.Contains(x.Token)).ExecuteDelete();
        }

        db.SaveChanges();

        if (failed > 0)
        {
            logger.LogWarning("彙總推播有 {Failed} 人送信失敗，水位維持不變，下一輪會重試", failed);
        }

        return new PushDigestResult(
            failed > 0 ? "PARTIAL_FAILURE" : "OK",
            scanned, pushed, failed, scanned - batch.Count, deadTokens.Count);
    }

    private void UpsertState(string userId, DateTimeOffset eventAt, DateTimeOffset nowUtc)
    {
        var state = db!.PushDigestStates.FirstOrDefault(x => x.UserId == userId);
        if (state == null)
        {
            db.PushDigestStates.Add(new PushDigestStateRecord
            {
                UserId = userId,
                LastEventAt = eventAt,
                LastPushedAt = nowUtc,
            });
            return;
        }

        state.LastEventAt = eventAt;
        state.LastPushedAt = nowUtc;
    }

    /// <summary>逐則訊息累加時的暫存。</summary>
    private sealed class MessageAccumulator
    {
        public int Count;
        public DateTimeOffset Latest = DateTimeOffset.MinValue;
        public HashSet<string> Rooms { get; } = [];
    }

    private sealed record MessageSummary(int Count, int Rooms, DateTimeOffset Latest, string RoomId);

    private sealed record NotificationSummary(string Type, int Count, DateTimeOffset Latest);

    private sealed record DigestPlan(
        string UserId,
        string Title,
        string Body,
        string? RoomId,
        DateTimeOffset EventAt)
    {
        public List<string> Tokens { get; init; } = [];
        public DateTimeOffset? LastPushedAt { get; init; }
    }

    /// <summary>
    /// 組一則彙總通知。關掉的類別直接不納入，連 EventAt 都不會被它汙染——
    /// 否則會出現「推播說有新通知，點進去卻什麼都沒有」。
    /// </summary>
    private static DigestPlan? BuildPlan(
        string userId, UserSettingRecord? setting, MessageSummary? messages, List<NotificationSummary> notifications)
    {
        var wantMessage = setting?.NotifyMessage ?? true;
        var usableMessages = wantMessage ? messages : null;

        var usableNotifications = notifications
            .Where(n => n.Type switch
            {
                "MATCH" => setting?.NotifyMatch ?? true,
                "RANT_REPLY" => setting?.NotifyRant ?? true,
                _ => true,
            })
            .ToList();

        if (usableMessages == null && usableNotifications.Count == 0) return null;

        var eventAt = usableMessages?.Latest ?? DateTimeOffset.MinValue;
        foreach (var n in usableNotifications)
        {
            if (n.Latest > eventAt) eventAt = n.Latest;
        }

        var parts = new List<string>();
        if (usableMessages != null)
        {
            parts.Add(usableMessages.Rooms > 1
                ? $"{usableMessages.Rooms} 個對話共 {usableMessages.Count} 則新訊息"
                : $"{usableMessages.Count} 則新訊息");
        }

        var notificationCount = usableNotifications.Sum(x => x.Count);
        if (notificationCount > 0)
        {
            parts.Add($"{notificationCount} 則新通知");
        }

        return new DigestPlan(
            userId,
            "同頻 Today",
            $"你有 {string.Join("、", parts)}",
            // 只有一個對話時直接帶房號，點開就進那間房
            usableMessages is { Rooms: 1 } ? usableMessages.RoomId : null,
            eventAt);
    }
}
