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

    public ChatMessage SendMessage(string chatRoomId, string senderId, string content)
    {
        if (db != null)
        {
            var messageRecord = new ChatMessageRecord
            {
                Id = $"message_{Guid.NewGuid():N}",
                ChatRoomId = chatRoomId,
                SenderId = senderId,
                Content = content.Trim()
            };
            db.ChatMessages.Add(messageRecord);
            db.SaveChanges();
            return messageRecord.ToDomain();
        }

        var message = new ChatMessage
        {
            Id = $"message_{Guid.NewGuid():N}",
            ChatRoomId = chatRoomId,
            SenderId = senderId,
            Content = content.Trim()
        };
        return storage.InsertOne("chatMessages", message);
    }

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
