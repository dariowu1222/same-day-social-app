using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    [HttpGet]
    public ActionResult<ApiResponse<List<Notification>>> Get([FromQuery] string? cursor, [FromQuery] int limit = 20)
    {
        DateTimeOffset? c = DateTimeOffset.TryParse(cursor, out var parsed) ? parsed : null;
        return ApiResponse<List<Notification>>.Ok(notifications.List(CallerId, c, limit));
    }

    [HttpGet("unread-count")]
    public ActionResult<ApiResponse<int>> UnreadCount()
        => ApiResponse<int>.Ok(notifications.UnreadCount(CallerId));

    // 開啟通知中心即全部標記已讀；回傳本次標記筆數
    [HttpPut("read")]
    public ActionResult<ApiResponse<int>> MarkAllRead()
        => ApiResponse<int>.Ok(notifications.MarkAllRead(CallerId));
}
