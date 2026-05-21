using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Application.Services;

public sealed class ChatService(JsonStorageService storage)
{
    public List<ChatRoom> GetRoomsByUser(string userId)
    {
        return storage.ReadCollection<ChatRoom>("chatRooms")
            .Where(x => x.UserIds.Contains(userId))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public ChatRoom CreateRoom(List<string> userIds, string sourceType, string sourceId)
    {
        var orderedUserIds = userIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Order().ToList();
        var existing = storage.ReadCollection<ChatRoom>("chatRooms")
            .FirstOrDefault(x => x.SourceId == sourceId && x.UserIds.Order().SequenceEqual(orderedUserIds));

        if (existing != null)
        {
            return existing;
        }

        var room = new ChatRoom
        {
            Id = $"chat_{Guid.NewGuid():N}",
            UserIds = orderedUserIds,
            SourceType = sourceType,
            SourceId = sourceId
        };
        return storage.InsertOne("chatRooms", room);
    }

    public List<ChatMessage> GetMessages(string chatRoomId)
    {
        return storage.ReadCollection<ChatMessage>("chatMessages")
            .Where(x => x.ChatRoomId == chatRoomId)
            .OrderBy(x => x.CreatedAt)
            .ToList();
    }

    public ChatMessage SendMessage(string chatRoomId, string senderId, string content)
    {
        var message = new ChatMessage
        {
            Id = $"message_{Guid.NewGuid():N}",
            ChatRoomId = chatRoomId,
            SenderId = senderId,
            Content = content.Trim()
        };
        return storage.InsertOne("chatMessages", message);
    }
}
