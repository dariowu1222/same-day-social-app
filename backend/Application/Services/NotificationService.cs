using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Application.Services;

/// <summary>
/// 為什麼這裡沒有帳號狀態的過濾（盤點過，刻意不加）：
///
/// 收件者永遠是呼叫者自己（List / UnreadCount / MarkAllRead 都拿 token 的 id），
/// 而非 ACTIVE 的帳號在 Program.cs 的中介軟體就被 401 擋下，讀不到任何一則。
/// Add 寫給一個已經註銷的收件者只是多幾列沒人看的資料，為此每則通知多付一趟
/// 資料庫往返並不划算——而通知本來就是寫入失敗也要默默吞掉的次要功能。
///
/// 觸發者那一側擋不了也不打算擋：這張表沒有存「是誰觸發的」，只有標題與一段
/// 內容摘要。要擋得加欄位，而擋到的也只是一則已經送達的舊通知；點進去之後
/// 那篇貼文／那個人的檔案該不該看得到，由 RantService 與 ProfileController 決定。
/// </summary>
public sealed class NotificationService
{
    private readonly JsonStorageService storage;
    private readonly AppDbContext? db;

    public NotificationService(JsonStorageService storage, IServiceProvider services)
    {
        this.storage = storage;
        db = services.GetService<AppDbContext>();
    }

    // 由其他 service 在事件發生時呼叫；收件者為空或等於觸發者時不寫入（不通知自己）。
    // best-effort：通知寫入失敗（如表尚未建立）絕不可拖垮觸發它的核心動作（按讚/回覆）。
    public void Add(string recipientId, string type, string title, string body, string? linkType = null, string? linkId = null)
    {
        if (string.IsNullOrWhiteSpace(recipientId)) return;

        var notification = new Notification
        {
            Id = $"notif_{Guid.NewGuid():N}",
            RecipientId = recipientId,
            Type = type,
            Title = title,
            Body = body,
            LinkType = linkType,
            LinkId = linkId,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            if (db != null)
            {
                db.Notifications.Add(notification.ToRecord());
                db.SaveChanges();
                return;
            }

            storage.InsertOne("notifications", notification);
        }
        catch
        {
            // 通知是次要功能，寫入失敗就放棄，不影響核心流程。
        }
    }

    public List<Notification> List(string recipientId, DateTimeOffset? cursor = null, int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);
        if (db != null)
        {
            var query = db.Notifications.AsNoTracking().Where(x => x.RecipientId == recipientId);
            if (cursor.HasValue) query = query.Where(x => x.CreatedAt < cursor.Value);
            return query
                .OrderByDescending(x => x.CreatedAt)
                .Take(limit)
                .ToList()
                .Select(x => x.ToDomain())
                .ToList();
        }

        return storage.ReadCollection<Notification>("notifications")
            .Where(x => x.RecipientId == recipientId && (!cursor.HasValue || x.CreatedAt < cursor.Value))
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToList();
    }

    public int UnreadCount(string recipientId)
    {
        if (db != null)
        {
            return db.Notifications.AsNoTracking().Count(x => x.RecipientId == recipientId && !x.IsRead);
        }

        return storage.ReadCollection<Notification>("notifications")
            .Count(x => x.RecipientId == recipientId && !x.IsRead);
    }

    // 開啟通知中心即全部標記已讀；回傳本次標記的筆數。
    public int MarkAllRead(string recipientId)
    {
        if (db != null)
        {
            return db.Notifications
                .Where(x => x.RecipientId == recipientId && !x.IsRead)
                .ExecuteUpdate(s => s.SetProperty(x => x.IsRead, true));
        }

        var marked = 0;
        var all = storage.ReadCollection<Notification>("notifications");
        foreach (var n in all.Where(x => x.RecipientId == recipientId && !x.IsRead))
        {
            n.IsRead = true;
            marked++;
        }
        if (marked > 0) storage.WriteCollection("notifications", all);
        return marked;
    }
}
