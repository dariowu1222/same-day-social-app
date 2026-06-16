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

    public List<ChatMessage> GetMessages(string chatRoomId)
    {
        if (db != null)
        {
            return db.ChatMessages.AsNoTracking()
                .Where(x => x.ChatRoomId == chatRoomId)
                .OrderBy(x => x.CreatedAt)
                .ToList()
                .Select(x => x.ToDomain())
                .ToList();
        }

        return storage.ReadCollection<ChatMessage>("chatMessages")
            .Where(x => x.ChatRoomId == chatRoomId)
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
            if (pinned.HasValue) r.Pinned = pinned.Value;
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
        Muted = r.Muted
    };

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
