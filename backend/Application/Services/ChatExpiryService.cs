using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Application.Services;

/// <summary>
/// 聊天室閒置到期。由排程呼叫（見 .github/workflows/chat-expiry.yml）。
///
/// 兩段式，而且第二段的前提是第一段已經發生過：
///
///   第 N-1 天  → 雙方各收到一則站內通知
///   第 N 天起  → 通知已經發出滿 24 小時，才把房間鎖定
///
/// 為什麼要分兩段、而且要檢查「通知已滿 24 小時」：
/// 排程漏跑一天是會發生的（GitHub Actions 的 schedule 沒有 SLA）。沒有這個前提，
/// 房間會從第 29 天直接跳到第 31 天被鎖，使用者在毫無預警的情況下發現聊天室不能
/// 再回話。改成「沒通知過就不准鎖」之後，漏跑最多只會讓鎖定晚一天，不會讓它
/// 無聲發生。
///
/// 為什麼鎖定而不是把 chat_room_users 刪掉：
/// 房間要留在列表上，點進去看得到歷史、看得到為什麼被鎖，只剩「退出聊天室」
/// 一個動作。真正的離開由本人按下去（ChatService.LeaveRoom）。排程代為刪除的話，
/// 聊天室會無聲消失，使用者只會以為是壞掉了。
///
/// 為什麼不需要在「送出訊息」那條路上清掉 expiry_notified_at：
/// 判斷用的是「通知時間有沒有比最後一次活動晚」。有人回話之後，最後活動時間
/// 就跑到通知時間之後，這間房自然又變成「沒通知過」的狀態，下一輪會重新通知。
/// 熱路徑一個字都不必改。
/// </summary>
public sealed class ChatExpiryService(
    IServiceProvider services,
    AppSettingsService settings,
    NotificationService notifications,
    ILogger<ChatExpiryService> logger)
{
    private readonly AppDbContext? db = services.GetService<AppDbContext>();

    public sealed record Outcome(int Notified, int Expired, int Scanned, bool StorageReady);

    public Outcome Run()
    {
        if (db == null) return new Outcome(0, 0, 0, false);

        var idleDays = settings.IdleDays;
        var floor = settings.ExpirySince;
        var now = DateTimeOffset.UtcNow;
        var notifyAt = now.AddDays(-(idleDays - 1));
        var expireAt = now.AddDays(-idleDays);

        try
        {
            // 還沒鎖定的房間才要看。一次把最後訊息時間撈齊再比對——逐房查會變成
            // N+1，而這支是排程，寧可一次吃完。
            var rooms = db.ChatRooms.AsNoTracking()
                .Where(x => x.ExpiredAt == null)
                .Select(x => new { x.Id, x.CreatedAt, x.ExpiryNotifiedAt })
                .ToList();
            if (rooms.Count == 0) return new Outcome(0, 0, 0, true);

            var ids = rooms.Select(x => x.Id).ToList();
            var lastMessage = db.ChatMessages.AsNoTracking()
                .Where(m => ids.Contains(m.ChatRoomId))
                .GroupBy(m => m.ChatRoomId)
                .Select(g => new { RoomId = g.Key, At = g.Max(m => m.CreatedAt) })
                .ToList()
                .ToDictionary(x => x.RoomId, x => x.At);

            // 最後一次活動。三者取最晚：
            //   - 最後一則訊息
            //   - 房間建立時間（從來沒人說過話的房間，從開房那天算）
            //   - chat.expiry_since（既有資料的寬限下限，見 AppSettingsService）
            DateTimeOffset LastActivity(string roomId, DateTimeOffset createdAt)
            {
                var at = lastMessage.TryGetValue(roomId, out var msg) && msg > createdAt ? msg : createdAt;
                return floor is { } f && f > at ? f : at;
            }

            var toNotify = new List<string>();
            var toExpire = new List<string>();

            foreach (var room in rooms)
            {
                var activity = LastActivity(room.Id, room.CreatedAt);

                // 通知時間比最後活動還早 = 那則通知是上一輪沉寂發的，中間有人回過話，
                // 這一輪要重新通知。
                var notifiedThisRound = room.ExpiryNotifiedAt is { } n && n >= activity;

                if (!notifiedThisRound)
                {
                    if (activity <= notifyAt) toNotify.Add(room.Id);
                    continue;
                }

                if (activity <= expireAt && room.ExpiryNotifiedAt <= now.AddHours(-24))
                {
                    toExpire.Add(room.Id);
                }
            }

            var notified = Notify(toNotify, idleDays, now);
            var expired = Expire(toExpire, now);

            if (notified > 0 || expired > 0)
            {
                logger.LogInformation(
                    "聊天室到期處理：掃描 {Scanned} 間，通知 {Notified} 間，鎖定 {Expired} 間（閒置門檻 {IdleDays} 天）",
                    rooms.Count, notified, expired, idleDays);
            }

            return new Outcome(notified, expired, rooms.Count, true);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UndefinedColumn
                                                 || ex.SqlState == Npgsql.PostgresErrorCodes.UndefinedTable)
        {
            // migration 還沒跑。這支是排程呼叫的，回報狀態就好，不要讓整個端點 500
            // ——否則 GitHub Actions 會每小時紅一次，真正的錯誤反而被淹掉。
            logger.LogWarning("聊天室到期處理：資料表還沒準備好（{SqlState}），這次跳過", ex.SqlState);
            return new Outcome(0, 0, 0, false);
        }
    }

    private int Notify(List<string> roomIds, int idleDays, DateTimeOffset now)
    {
        if (db == null || roomIds.Count == 0) return 0;

        var members = db.ChatRoomUsers.AsNoTracking()
            .Where(x => roomIds.Contains(x.ChatRoomId))
            .Select(x => new { x.ChatRoomId, x.UserId })
            .ToList();

        foreach (var member in members)
        {
            notifications.Add(
                member.UserId,
                "CHAT_EXPIRING",
                "聊天室明天就會關閉",
                $"你們已經 {idleDays - 1} 天沒有往來。再沒有訊息的話，明天這個聊天室就會關閉，只能退出。",
                linkType: "chat",
                linkId: member.ChatRoomId);
        }

        var rows = db.ChatRooms.Where(x => roomIds.Contains(x.Id)).ToList();
        foreach (var room in rows) room.ExpiryNotifiedAt = now;
        db.SaveChanges();
        return rows.Count;
    }

    private int Expire(List<string> roomIds, DateTimeOffset now)
    {
        if (db == null || roomIds.Count == 0) return 0;

        var rows = db.ChatRooms.Where(x => roomIds.Contains(x.Id)).ToList();
        foreach (var room in rows) room.ExpiredAt = now;
        db.SaveChanges();
        return rows.Count;
    }
}
