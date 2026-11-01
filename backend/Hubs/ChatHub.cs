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

    /// <summary>
    /// 連上就自動加入自己的個人群組。
    ///
    /// 房間群組（chat-room:）只有在聊天頁才 join，而未讀徽章在任何分頁都要會動，
    /// 所以「某房有新訊息」這種通知走個人群組推——前端不必知道任何 room id，
    /// 也省掉 JoinRooms 每次付的成員查詢。
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        if (!string.IsNullOrEmpty(CallerId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(CallerId));
        }

        await base.OnConnectedAsync();
    }

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

    public static string UserGroup(string userId) => $"chat-user:{userId}";
}
