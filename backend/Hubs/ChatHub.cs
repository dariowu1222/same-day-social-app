using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SameDaySocialApp.Application.Services;

namespace SameDaySocialApp.Hubs;

[Authorize]
public sealed class ChatHub(ChatService chatService) : Hub
{
    private string CallerId => Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? Context.User?.FindFirst("sub")?.Value
                               ?? "";

    public async Task JoinRooms(List<string> roomIds)
    {
        var allowedRoomIds = chatService.GetRoomsByUser(CallerId)
            .Select(x => x.Id)
            .ToHashSet();

        foreach (var roomId in roomIds.Where(allowedRoomIds.Contains).Distinct())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
        }
    }

    public async Task LeaveRoom(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(roomId));
    }

    public static string RoomGroup(string roomId) => $"chat-room:{roomId}";
}
