using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/chats")]
[Authorize]
public sealed class ChatsController(ChatService chatService) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    [HttpGet("user/{userId}")]
    public ActionResult<ApiResponse<List<ChatRoom>>> GetRooms(string userId)
    {
        if (CallerId != userId) return Forbid();
        return ApiResponse<List<ChatRoom>>.Ok(chatService.GetRoomsByUser(userId));
    }

    [HttpPost]
    public ActionResult<ApiResponse<ChatRoom>> Create([FromBody] CreateChatRoomRequest request)
    {
        if (!request.UserIds.Contains(CallerId)) return Forbid();
        return ApiResponse<ChatRoom>.Ok(chatService.CreateRoom(request.UserIds, request.SourceType, request.SourceId));
    }

    [HttpGet("{chatRoomId}/messages")]
    public ActionResult<ApiResponse<List<ChatMessage>>> GetMessages(string chatRoomId)
    {
        var rooms = chatService.GetRoomsByUser(CallerId);
        if (rooms.All(r => r.Id != chatRoomId)) return Forbid();
        return ApiResponse<List<ChatMessage>>.Ok(chatService.GetMessages(chatRoomId));
    }

    [HttpPost("{chatRoomId}/messages")]
    public ActionResult<ApiResponse<ChatMessage>> SendMessage(string chatRoomId, [FromBody] SendMessageRequest request)
    {
        var rooms = chatService.GetRoomsByUser(CallerId);
        if (rooms.All(r => r.Id != chatRoomId)) return Forbid();
        return ApiResponse<ChatMessage>.Ok(chatService.SendMessage(
            chatRoomId, CallerId, request.Content,
            request.QuotedMessageId, request.QuotedSenderName, request.QuotedContent));
    }

    [HttpPost("{chatRoomId}/messages/{messageId}/recall")]
    public ActionResult<ApiResponse<ChatMessage>> RecallMessage(string chatRoomId, string messageId)
    {
        var rooms = chatService.GetRoomsByUser(CallerId);
        if (rooms.All(r => r.Id != chatRoomId)) return Forbid();
        var recalled = chatService.RecallMessage(messageId, CallerId);
        if (recalled == null) return BadRequest(ApiResponse<ChatMessage>.Fail("MESSAGE_RECALL_DENIED", "訊息無法收回（僅限本人發出後 2 分鐘內）。"));
        return ApiResponse<ChatMessage>.Ok(recalled);
    }

    // 房間個人設定（備註名 / 釘選 / 靜音）
    [HttpGet("user/{userId}/settings")]
    public ActionResult<ApiResponse<List<ChatMemberSetting>>> GetSettings(string userId)
    {
        if (CallerId != userId) return Forbid();
        return ApiResponse<List<ChatMemberSetting>>.Ok(chatService.GetMemberSettings(userId));
    }

    [HttpPut("{chatRoomId}/settings")]
    public ActionResult<ApiResponse<ChatMemberSetting>> UpdateSettings(string chatRoomId, [FromBody] UpdateChatSettingRequest request)
    {
        if (!IsMember(chatRoomId)) return Forbid();
        return ApiResponse<ChatMemberSetting>.Ok(
            chatService.UpdateMemberSetting(CallerId, chatRoomId, request.NoteName, request.Pinned, request.Muted));
    }

    // 離開聊天室
    [HttpPost("{chatRoomId}/leave")]
    public ActionResult<ApiResponse<bool>> Leave(string chatRoomId)
    {
        if (!IsMember(chatRoomId)) return Forbid();
        chatService.LeaveRoom(CallerId, chatRoomId);
        return ApiResponse<bool>.Ok(true);
    }

    // 離開並封鎖對方
    [HttpPost("{chatRoomId}/block")]
    public ActionResult<ApiResponse<bool>> Block(string chatRoomId)
    {
        var other = OtherUserId(chatRoomId);
        if (other == null) return Forbid();
        chatService.BlockUser(CallerId, other);
        chatService.LeaveRoom(CallerId, chatRoomId);
        return ApiResponse<bool>.Ok(true);
    }

    // 離開並檢舉對方
    [HttpPost("{chatRoomId}/report")]
    public ActionResult<ApiResponse<bool>> Report(string chatRoomId, [FromBody] ReportChatRequest? request)
    {
        var other = OtherUserId(chatRoomId);
        if (other == null) return Forbid();
        chatService.ReportUser(CallerId, other, chatRoomId, request?.Reason);
        chatService.LeaveRoom(CallerId, chatRoomId);
        return ApiResponse<bool>.Ok(true);
    }

    private bool IsMember(string chatRoomId)
        => chatService.GetRoomsByUser(CallerId).Any(r => r.Id == chatRoomId);

    private string? OtherUserId(string chatRoomId)
        => chatService.GetRoomsByUser(CallerId)
            .FirstOrDefault(r => r.Id == chatRoomId)?
            .UserIds.FirstOrDefault(u => u != CallerId);
}

public sealed record CreateChatRoomRequest(List<string> UserIds, string SourceType, string SourceId);
public sealed record SendMessageRequest(string Content, string? QuotedMessageId = null, string? QuotedSenderName = null, string? QuotedContent = null);
public sealed record UpdateChatSettingRequest(string? NoteName = null, bool? Pinned = null, bool? Muted = null);
public sealed record ReportChatRequest(string? Reason = null);
