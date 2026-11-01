using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Application.Services;

public sealed class ChatService
{
    private readonly JsonStorageService storage;
    private readonly AppDbContext? db;

    public ChatService(JsonStorageService storage, IServiceProvider services)
    {
        this.storage = storage;
        db = services.GetService<AppDbContext>();
    }

    /// <summary>
    /// 房間成員檢查：單一 EXISTS 查詢。
    /// 用於各端點的權限守門，取代原本呼叫 GetRoomsByUser（3 次查詢＋組裝整份房間清單），
    /// 每個請求省下 2 次 DB 往返——遠端 DB 下這是聊天延遲的主因之一。
    /// </summary>
    public bool IsRoomMember(string userId, string chatRoomId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(chatRoomId))
        {
            return false;
        }

        if (db != null)
        {
            return db.ChatRoomUsers.AsNoTracking()
                .Any(x => x.ChatRoomId == chatRoomId && x.UserId == userId);
        }

        return storage.ReadCollection<ChatRoom>("chatRooms")
            .Any(x => x.Id == chatRoomId && x.UserIds.Contains(userId));
    }

    public List<ChatRoom> GetRoomsByUser(string userId)
    {
        if (db != null)
        {
            var roomIds = db.ChatRoomUsers.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.ChatRoomId)
                .ToList();
            var rooms = db.ChatRooms.AsNoTracking()
                .Where(x => roomIds.Contains(x.Id))
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
            var users = db.ChatRoomUsers.AsNoTracking()
                .Where(x => roomIds.Contains(x.ChatRoomId))
                .ToList()
                .GroupBy(x => x.ChatRoomId)
                .ToDictionary(x => x.Key, x => x.AsEnumerable());

            return rooms
                .Select(x => x.ToDomain(
                    users.GetValueOrDefault(x.Id, Array.Empty<ChatRoomUserRecord>())))
                .ToList();
        }

        return storage.ReadCollection<ChatRoom>("chatRooms")
            .Where(x => x.UserIds.Contains(userId))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public ChatRoom CreateRoom(List<string> userIds, string sourceType, string sourceId)
    {
        var orderedUserIds = userIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Order().ToList();

        if (db != null)
        {
            var candidateRoomIds = db.ChatRooms.AsNoTracking()
                .Where(x => x.SourceId == sourceId && x.SourceType == sourceType)
                .Select(x => x.Id)
                .ToList();
            foreach (var candidateRoomId in candidateRoomIds)
            {
                var existingUsers = db.ChatRoomUsers.AsNoTracking()
                    .Where(x => x.ChatRoomId == candidateRoomId)
                    .Select(x => x.UserId)
                    .Order()
                    .ToList();
                if (existingUsers.SequenceEqual(orderedUserIds))
                {
                    return BuildRoom(candidateRoomId)!;
                }
            }

            var room = new ChatRoomRecord
            {
                Id = $"chat_{Guid.NewGuid():N}",
                SourceType = sourceType,
                SourceId = sourceId
            };
            db.ChatRooms.Add(room);
            db.ChatRoomUsers.AddRange(orderedUserIds.Select(userId => new ChatRoomUserRecord
            {
                Id = $"chat_user_{Guid.NewGuid():N}",
                ChatRoomId = room.Id,
                UserId = userId
            }));
            db.SaveChanges();
            return BuildRoom(room.Id)!;
        }

        var existing = storage.ReadCollection<ChatRoom>("chatRooms")
            .FirstOrDefault(x => x.SourceId == sourceId && x.UserIds.Order().SequenceEqual(orderedUserIds));

        if (existing != null)
        {
            return existing;
        }

        var fileRoom = new ChatRoom
        {
            Id = $"chat_{Guid.NewGuid():N}",
            UserIds = orderedUserIds,
            SourceType = sourceType,
            SourceId = sourceId
        };
        return storage.InsertOne("chatRooms", fileRoom);
    }

    // 一次最多回傳的訊息數：只取最近這批，避免老房間把整段歷史全撈出來。
    private const int DefaultMessageLimit = 50;
    private const int MaxMessageLimit = 200;

    /// <summary>
    /// 取聊天室訊息：預設只回最近 <see cref="DefaultMessageLimit"/> 則。
    /// 先以時間倒序取最新一批，再正序回傳給前端顯示。
    /// </summary>
    public List<ChatMessage> GetMessages(string chatRoomId, int limit = DefaultMessageLimit)
    {
        limit = Math.Clamp(limit, 1, MaxMessageLimit);

        if (db != null)
        {
            return db.ChatMessages.AsNoTracking()
                .Where(x => x.ChatRoomId == chatRoomId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(limit)
                .ToList()
                .OrderBy(x => x.CreatedAt)
                .Select(x => x.ToDomain())
                .ToList();
        }

        return storage.ReadCollection<ChatMessage>("chatMessages")
            .Where(x => x.ChatRoomId == chatRoomId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .OrderBy(x => x.CreatedAt)
            .ToList();
    }

    public ChatMessage SendMessage(
        string chatRoomId, string senderId, string content,
        string? quotedMessageId = null, string? quotedSenderName = null, string? quotedContent = null)
    {
        var id = $"message_{Guid.NewGuid():N}";

        if (db != null)
        {
            var messageRecord = new ChatMessageRecord
            {
                Id = id,
                ChatRoomId = chatRoomId,
                SenderId = senderId,
                Content = content.Trim(),
                QuotedMessageId = quotedMessageId,
                QuotedSenderName = quotedSenderName,
                QuotedContent = quotedContent
            };
            db.ChatMessages.Add(messageRecord);
            db.SaveChanges();
            return messageRecord.ToDomain();
        }

        var message = new ChatMessage
        {
            Id = id,
            ChatRoomId = chatRoomId,
            SenderId = senderId,
            Content = content.Trim(),
            QuotedMessageId = quotedMessageId,
            QuotedSenderName = quotedSenderName,
            QuotedContent = quotedContent
        };
        return storage.InsertOne("chatMessages", message);
    }

    // 收回訊息：限本人、限發出後 2 分鐘內
    public ChatMessage? RecallMessage(string messageId, string callerId)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-2);

        if (db != null)
        {
            var record = db.ChatMessages.FirstOrDefault(x => x.Id == messageId);
            if (record == null || record.SenderId != callerId || record.CreatedAt < cutoff)
            {
                return null;
            }
            record.IsRecalled = true;
            record.Content = "";
            db.SaveChanges();
            return record.ToDomain();
        }

        var existing = storage.ReadCollection<ChatMessage>("chatMessages")
            .FirstOrDefault(x => x.Id == messageId);
        if (existing == null || existing.SenderId != callerId || existing.CreatedAt < cutoff)
        {
            return null;
        }
        var updated = storage.UpdateOne<ChatMessage>("chatMessages", messageId, m =>
        {
            m.IsRecalled = true;
            m.Content = "";
        });
        return updated;
    }

    public List<ChatMemberSetting> GetMemberSettings(string userId)
    {
        if (db != null)
        {
            return db.ChatMemberSettings.AsNoTracking()
                .Where(x => x.UserId == userId)
                .ToList()
                .Select(ToSetting)
                .ToList();
        }
        return storage.ReadCollection<ChatMemberSettingRecord>("chatMemberSettings")
            .Where(x => x.UserId == userId)
            .Select(ToSetting)
            .ToList();
    }

    public ChatMemberSetting UpdateMemberSetting(string userId, string chatRoomId, string? noteName, bool? pinned, bool? muted)
    {
        void Apply(ChatMemberSettingRecord r)
        {
            if (noteName != null) r.NoteName = string.IsNullOrWhiteSpace(noteName) ? null : noteName.Trim();
            if (pinned.HasValue)
            {
                // 只有「從沒釘變成釘」才記時間，重複送 pinned=true 不會把自己頂到最前面。
                // 取消釘選時清空，下次再釘就是新的時間。
                if (pinned.Value && !r.Pinned) r.PinnedAt = DateTimeOffset.UtcNow;
                else if (!pinned.Value) r.PinnedAt = null;
                r.Pinned = pinned.Value;
            }
            if (muted.HasValue) r.Muted = muted.Value;
            r.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (db != null)
        {
            var rec = db.ChatMemberSettings.FirstOrDefault(x => x.UserId == userId && x.ChatRoomId == chatRoomId);
            if (rec == null)
            {
                rec = new ChatMemberSettingRecord { Id = $"cms_{Guid.NewGuid():N}", UserId = userId, ChatRoomId = chatRoomId };
                Apply(rec);
                db.ChatMemberSettings.Add(rec);
            }
            else
            {
                Apply(rec);
            }
            db.SaveChanges();
            return ToSetting(rec);
        }

        var list = storage.ReadCollection<ChatMemberSettingRecord>("chatMemberSettings");
        var item = list.FirstOrDefault(x => x.UserId == userId && x.ChatRoomId == chatRoomId);
        if (item == null)
        {
            item = new ChatMemberSettingRecord { Id = $"cms_{Guid.NewGuid():N}", UserId = userId, ChatRoomId = chatRoomId };
            Apply(item);
            storage.InsertOne("chatMemberSettings", item);
        }
        else
        {
            storage.UpdateOne<ChatMemberSettingRecord>("chatMemberSettings", item.Id, Apply);
            Apply(item);
        }
        return ToSetting(item);
    }

    public void BlockUser(string blockerId, string blockedId)
    {
        if (string.IsNullOrWhiteSpace(blockedId)) return;
        if (db != null)
        {
            if (!db.UserBlocks.Any(x => x.BlockerId == blockerId && x.BlockedId == blockedId))
            {
                db.UserBlocks.Add(new UserBlockRecord { Id = $"block_{Guid.NewGuid():N}", BlockerId = blockerId, BlockedId = blockedId });
                db.SaveChanges();
            }
            return;
        }
        var blocks = storage.ReadCollection<UserBlockRecord>("userBlocks");
        if (!blocks.Any(x => x.BlockerId == blockerId && x.BlockedId == blockedId))
        {
            storage.InsertOne("userBlocks", new UserBlockRecord { Id = $"block_{Guid.NewGuid():N}", BlockerId = blockerId, BlockedId = blockedId });
        }
    }

    public void ReportUser(string reporterId, string reportedUserId, string chatRoomId, string? reason)
    {
        var rec = new ChatReportRecord
        {
            Id = $"creport_{Guid.NewGuid():N}",
            ReporterId = reporterId,
            ReportedUserId = reportedUserId,
            ChatRoomId = chatRoomId,
            Reason = reason
        };
        if (db != null) { db.ChatReports.Add(rec); db.SaveChanges(); return; }
        storage.InsertOne("chatReports", rec);
    }

    public void LeaveRoom(string userId, string chatRoomId)
    {
        if (db != null)
        {
            var membership = db.ChatRoomUsers.Where(x => x.ChatRoomId == chatRoomId && x.UserId == userId).ToList();
            if (membership.Count > 0) { db.ChatRoomUsers.RemoveRange(membership); db.SaveChanges(); }
            return;
        }
        var rooms = storage.ReadCollection<ChatRoom>("chatRooms");
        var room = rooms.FirstOrDefault(x => x.Id == chatRoomId);
        if (room != null && room.UserIds.Remove(userId)) storage.WriteCollection("chatRooms", rooms);
    }

    private static ChatMemberSetting ToSetting(ChatMemberSettingRecord r) => new()
    {
        ChatRoomId = r.ChatRoomId,
        NoteName = r.NoteName,
        Pinned = r.Pinned,
        Muted = r.Muted,
        PinnedAt = r.PinnedAt
    };

    /// <summary>
    /// 聊天列表一次要的全部資料：房間、對方的暱稱與照片、最後一則訊息。
    ///
    /// 先前前端是「房間列表 → 每間房各打一次 /messages + 每個對方各打一次
    /// /profile」，30 間房就是 60 幾個請求。瀏覽器對同一來源只開 6 條連線，
    /// 實測單一請求 54-331ms，但擠在一起時中位數變成 4,353ms——95% 的時間
    /// 都在排隊。而那 60 個請求問的只是「最後一則訊息是什麼、對方是誰」。
    ///
    /// 這裡用 4 個查詢回答完：房間、成員、對方的使用者資料、每房最後一則訊息。
    /// 進房後仍走原本的 GetMessages，這支只服務列表。
    /// </summary>
    public List<ChatRoomOverview> GetRoomOverviews(string userId)
    {
        if (db == null)
        {
            return GetRoomsByUser(userId)
                .Select(room => new ChatRoomOverview(room, PeerOf(room, userId), null, null, null))
                .ToList();
        }

        var rooms = GetRoomsByUser(userId);
        if (rooms.Count == 0)
        {
            return [];
        }

        var roomIds = rooms.Select(x => x.Id).ToList();

        // 每間房的最後一則：用相關子查詢比對該房的最大 created_at，
        // 吃得到既有的 ix_chat_messages_room_created 索引。
        var lastMessages = db.ChatMessages.AsNoTracking()
            .Where(m => roomIds.Contains(m.ChatRoomId)
                        && m.CreatedAt == db.ChatMessages
                            .Where(x => x.ChatRoomId == m.ChatRoomId)
                            .Max(x => x.CreatedAt))
            .ToList()
            // 同一毫秒有兩則時會各回一筆，取其一即可
            .GroupBy(m => m.ChatRoomId)
            .ToDictionary(g => g.Key, g => g.First().ToDomain());

        var peerIds = rooms
            .Select(room => PeerOf(room, userId))
            .Where(id => id != null)
            .Select(id => id!)
            .Distinct()
            .ToList();

        // 列表只用得到暱稱與第一張照片，不要整包 User（那裡面有興趣、個性、
        // 外貌等幾十個欄位，而且照片可能是 base64）
        var peers = db.Users.AsNoTracking()
            .Where(u => peerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nickname, u.PhotoDataUrls })
            .ToList()
            .ToDictionary(
                u => u.Id,
                u => (Nickname: u.Nickname, Photo: u.PhotoDataUrls.FirstOrDefault()));

        return rooms.Select(room =>
        {
            var peerId = PeerOf(room, userId);
            var peer = peerId != null && peers.TryGetValue(peerId, out var p) ? p : default;
            return new ChatRoomOverview(
                room,
                peerId,
                peer.Nickname,
                peer.Photo,
                ToPreview(lastMessages.GetValueOrDefault(room.Id)));
        }).ToList();
    }

    /// <summary>
    /// 一次算完所有房間的未讀數。
    ///
    /// 水位是 COALESCE(last_read_at, joined_at)：沒標記過的房從加入時間起算。
    /// 自己發的不算、已收回的不算——收回後內容已清空，為它亮紅點沒有意義。
    ///
    /// 刻意拆成兩句單純的查詢，而不是一句 join + group by：後者要賭 EF 的
    /// 關聯聚合翻譯，翻不出來就退化成客戶端評估，把整張表拉進記憶體；
    /// 幾個並發請求就足以把執行緒池堵死（實測過）。
    /// 這裡兩句都吃得到既有索引，而且第二句只取兩個欄位。
    /// </summary>
    public Dictionary<string, int> CountUnread(string userId, List<string> roomIds)
    {
        if (db == null || roomIds.Count == 0)
        {
            return [];
        }

        var watermarks = db.ChatRoomUsers.AsNoTracking()
            .Where(u => u.UserId == userId && roomIds.Contains(u.ChatRoomId))
            .Select(u => new { u.ChatRoomId, At = u.LastReadAt ?? u.JoinedAt })
            .ToList()
            .ToDictionary(x => x.ChatRoomId, x => x.At);

        if (watermarks.Count == 0)
        {
            return [];
        }

        // 每間房各自受自己的水位限制，組成 (房號 = X AND 時間 > X 的水位) OR (...) 。
        //
        // 不能用「所有房間裡最早的水位」當共同下限：只要有一間房從沒被打開過
        // （水位退回 joined_at），那個下限就永遠不會前進，於是每次查詢都把這個人
        // 名下所有房間的全部歷史訊息撈回記憶體——而 /unread 在掛載、連上、重連、
        // 回前景都會被呼叫。
        var predicate = PredicateFalse<ChatMessageRecord>();
        foreach (var (roomId, watermark) in watermarks)
        {
            var id = roomId;
            var at = watermark;
            predicate = Or(predicate, m => m.ChatRoomId == id && m.CreatedAt > at);
        }

        return db.ChatMessages.AsNoTracking()
            .Where(m => m.SenderId != userId && !m.IsRecalled)
            .Where(predicate)
            .GroupBy(m => m.ChatRoomId)
            .Select(g => new { RoomId = g.Key, Count = g.Count() })
            .ToList()
            .ToDictionary(x => x.RoomId, x => x.Count);
    }

    /// <summary>一間房的成員 id。廣播「某房有新訊息」時要用來找收件人。</summary>
    public List<string> GetRoomMemberIds(string chatRoomId)
    {
        if (db != null)
        {
            return db.ChatRoomUsers.AsNoTracking()
                .Where(x => x.ChatRoomId == chatRoomId)
                .Select(x => x.UserId)
                .ToList();
        }

        return storage.ReadCollection<ChatRoom>("chatRooms")
            .FirstOrDefault(x => x.Id == chatRoomId)?.UserIds ?? [];
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> PredicateFalse<T>() => _ => false;

    /// <summary>把兩個述詞用 OR 接起來，參數重新繫結到同一個，EF 才翻得出單一 WHERE。</summary>
    private static System.Linq.Expressions.Expression<Func<T, bool>> Or<T>(
        System.Linq.Expressions.Expression<Func<T, bool>> left,
        System.Linq.Expressions.Expression<Func<T, bool>> right)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "x");
        var body = System.Linq.Expressions.Expression.OrElse(
            new ParameterRebinder(left.Parameters[0], parameter).Visit(left.Body),
            new ParameterRebinder(right.Parameters[0], parameter).Visit(right.Body));
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private sealed class ParameterRebinder(
        System.Linq.Expressions.ParameterExpression from,
        System.Linq.Expressions.ParameterExpression to) : System.Linq.Expressions.ExpressionVisitor
    {
        protected override System.Linq.Expressions.Expression VisitParameter(
            System.Linq.Expressions.ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }

    /// <summary>每個有未讀的房間與其未讀數。沒有未讀的房間不會出現在結果裡。</summary>
    public Dictionary<string, int> GetUnreadCounts(string userId)
    {
        if (db == null)
        {
            return [];
        }

        var roomIds = GetRoomsByUser(userId).Select(x => x.Id).ToList();
        return CountUnread(userId, roomIds);
    }

    /// <summary>
    /// 把一間房標成已讀。
    ///
    /// 水位取「這間房目前最後一則訊息的時間」而不是 now()：還在寫入途中、
    /// 尚未 commit 的訊息不會被誤標成已讀（它的 created_at 會大於這個水位）。
    /// 水位只前進不後退，所以重複呼叫、亂序抵達都安全。
    /// </summary>
    public bool MarkRoomRead(string chatRoomId, string userId)
    {
        if (db == null)
        {
            return true;
        }

        var watermark = db.ChatMessages.AsNoTracking()
            .Where(m => m.ChatRoomId == chatRoomId)
            .Max(m => (DateTimeOffset?)m.CreatedAt);

        if (watermark == null)
        {
            return true;
        }

        db.ChatRoomUsers
            .Where(x => x.ChatRoomId == chatRoomId
                        && x.UserId == userId
                        && (x.LastReadAt == null || x.LastReadAt < watermark))
            .ExecuteUpdate(s => s.SetProperty(x => x.LastReadAt, watermark));

        return true;
    }

    /// <summary>
    /// 把最後一則訊息折成「預覽」再回傳。
    ///
    /// 列表只會顯示一行字，但訊息內容可能是整串 base64——實測資料庫裡最長的
    /// 一則有 122,931 個字元。不折的話，列表為了印一句「你傳了一張圖片」
    /// 就要下載 120 KB。網址型的媒體也沒必要整串傳，前端的 formatMessagePreview
    /// 本來就看得懂 [image] / [audio] 這種標記。
    /// </summary>
    private static ChatMessage? ToPreview(ChatMessage? message)
    {
        if (message == null)
        {
            return null;
        }

        var content = message.Content.Trim();
        if (content.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) || IsMediaUrl(content, ImageExtensions))
        {
            content = "[image]";
        }
        else if (content.StartsWith("data:audio/", StringComparison.OrdinalIgnoreCase) || IsMediaUrl(content, AudioExtensions))
        {
            content = "[audio]";
        }
        else if (content.Length > PreviewLength)
        {
            content = content[..PreviewLength];
        }

        return new ChatMessage
        {
            Id = message.Id,
            ChatRoomId = message.ChatRoomId,
            SenderId = message.SenderId,
            Content = content,
            CreatedAt = message.CreatedAt,
            IsRecalled = message.IsRecalled,
            // 引用欄位列表用不到，不帶出去
        };
    }

    private const int PreviewLength = 120;
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private static readonly string[] AudioExtensions = [".mp3", ".webm", ".wav", ".m4a", ".ogg"];

    private static bool IsMediaUrl(string content, string[] extensions)
    {
        if (!content.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !content.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var path = content.Split('?')[0];
        return extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static string? PeerOf(ChatRoom room, string userId)
        => room.UserIds.FirstOrDefault(id => id != userId);

    private ChatRoom? BuildRoom(string roomId)
    {
        if (db == null)
        {
            return null;
        }

        var room = db.ChatRooms.AsNoTracking().FirstOrDefault(x => x.Id == roomId);
        if (room == null)
        {
            return null;
        }

        var users = db.ChatRoomUsers.AsNoTracking()
            .Where(x => x.ChatRoomId == roomId)
            .ToList();
        return room.ToDomain(users);
    }
}

/// <summary>
/// 聊天列表一次要的資料。對方不存在或房間還沒有訊息時，後三個欄位會是 null，
/// 前端照既有的退場邏輯處理（暱稱退回「同頻對話 xxxx」、預覽顯示「還沒開始聊天」）。
/// </summary>
public sealed record ChatRoomOverview(
    ChatRoom Room,
    string? PeerUserId,
    string? PeerNickname,
    string? PeerPhotoUrl,
    ChatMessage? LastMessage);
