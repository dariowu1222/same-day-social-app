using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/chats")]
public sealed class ChatsController(ChatService chatService) : ControllerBase
{
    [HttpGet("user/{userId}")]
    public ActionResult<ApiResponse<List<ChatRoom>>> GetRooms(string userId)
    {
        return ApiResponse<List<ChatRoom>>.Ok(chatService.GetRoomsByUser(userId));
    }

    [HttpPost]
    public ActionResult<ApiResponse<ChatRoom>> Create([FromBody] CreateChatRoomRequest request)
    {
        return ApiResponse<ChatRoom>.Ok(chatService.CreateRoom(request.UserIds, request.SourceType, request.SourceId));
    }

    [HttpGet("{chatRoomId}/messages")]
    public ActionResult<ApiResponse<List<ChatMessage>>> GetMessages(string chatRoomId)
    {
        return ApiResponse<List<ChatMessage>>.Ok(chatService.GetMessages(chatRoomId));
    }

    [HttpPost("{chatRoomId}/messages")]
    public ActionResult<ApiResponse<ChatMessage>> SendMessage(string chatRoomId, [FromBody] SendMessageRequest request)
    {
        return ApiResponse<ChatMessage>.Ok(chatService.SendMessage(chatRoomId, request.SenderId, request.Content));
    }
}

public sealed record CreateChatRoomRequest(List<string> UserIds, string SourceType, string SourceId);
public sealed record SendMessageRequest(string SenderId, string Content);
