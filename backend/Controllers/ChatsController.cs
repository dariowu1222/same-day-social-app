using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Application.Validation;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Hubs;
using SameDaySocialApp.Infrastructure.Media;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/chats")]
[Authorize]
public sealed class ChatsController(ChatService chatService, MediaUrlPolicy mediaUrls, IHubContext<ChatHub> chatHub) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    [HttpGet("user/{userId}")]
    public ActionResult<ApiResponse<List<ChatRoom>>> GetRooms(string userId)
    {
        if (CallerId != userId) return Forbid();
        return ApiResponse<List<ChatRoom>>.Ok(chatService.GetRoomsByUser(userId));
    }

    /// <summary>
    /// 聊天列表用：一次回所有房間、對方資料與最後一則訊息。
    /// 舊的 /user/{userId} 與 /{roomId}/messages 原樣保留，已上架的版本不受影響。
    /// </summary>
    [HttpGet("user/{userId}/overview")]
    public ActionResult<ApiResponse<List<ChatRoomOverview>>> GetOverview(string userId)
    {
        if (CallerId != userId) return Forbid();
        return ApiResponse<List<ChatRoomOverview>>.Ok(chatService.GetRoomOverviews(userId));
    }

    [HttpGet("{chatRoomId}/messages")]
    public ActionResult<ApiResponse<List<ChatMessage>>> GetMessages(string chatRoomId, [FromQuery] int? limit = null)
    {
        if (!IsMember(chatRoomId)) return Forbid();
        return ApiResponse<List<ChatMessage>>.Ok(
            limit.HasValue ? chatService.GetMessages(chatRoomId, limit.Value) : chatService.GetMessages(chatRoomId));
    }

    /// <summary>
    /// 聊天對象額度。滑卡頁在開始滑之前先問一次，額滿就先跳提示——
    /// 讓人滑完十張才發現配不到，比一開始就講清楚糟得多。
    ///
    /// used 只算「還沒到期」的房間，與 CreateRoom 的判斷同一條規則。
    /// </summary>
    [HttpGet("partner-quota")]
    public ActionResult<ApiResponse<PartnerQuota>> GetPartnerQuota()
    {
        var used = chatService.CountActivePartners(CallerId);
        var limit = chatService.PartnerLimit;
        return ApiResponse<PartnerQuota>.Ok(new PartnerQuota(used, limit, used >= limit));
    }

    [EnableRateLimiting("write")]
    [HttpPost("{chatRoomId}/messages")]
    public async Task<ActionResult<ApiResponse<ChatMessage>>> SendMessage(string chatRoomId, [FromBody] SendMessageRequest request)
    {
        if (!IsMember(chatRoomId)) return Forbid();

        // Content 宣告為不可為 null，但 JSON 送 null 照樣會進來，
        // 而服務層第一件事就是 content.Trim()——以前這裡是一個 500。
        var content = request.Content ?? "";
        if (content.Length > InputLimits.MaxMessageLength)
        {
            return BadRequest(ApiResponse<ChatMessage>.Fail(
                "FIELD_TOO_LONG", $"訊息最多 {InputLimits.MaxMessageLength} 字。"));
        }

        // 圖片與語音訊息的「內容」就是一串網址，而前端看到像媒體的字串會直接
        // <img src> / <audio src> 下去。網址指向攻擊者的伺服器，對方一打開對話，
        // 瀏覽器就替他送出一次請求——IP、裝置、確切的上線時間全都有了。
        // 純文字與一般連結不受影響（見 MediaUrlPolicy）。
        if (!mediaUrls.IsAllowedAsMessage(content))
        {
            return BadRequest(ApiResponse<ChatMessage>.Fail(
                "INVALID_MEDIA_URL", "圖片或語音必須是從這個 App 上傳的。"));
        }

        // 檢舉經後台審核成立之後這間房就唯讀了。前端已經把輸入列換成說明，
        // 但那只是畫面——舊的分頁、送到一半的請求都還是會打進來。
        if (chatService.GetFrozenRoomIds(CallerId, [chatRoomId]).Count > 0)
        {
            return BadRequest(ApiResponse<ChatMessage>.Fail(
                "CHAT_ROOM_REPORTED", "你檢舉的對象已經被處置，這段對話無法再傳送訊息。"));
        }

        // 閒置到期也會讓房間唯讀。跟上面分開回報：兩者的原因完全不同，
        // 前端要據此顯示不一樣的說明，共用一個代碼就只能對使用者說錯話。
        if (chatService.GetExpiredRoomIds([chatRoomId]).Count > 0)
        {
            return BadRequest(ApiResponse<ChatMessage>.Fail(
                "CHAT_ROOM_EXPIRED", "這個聊天室因為太久沒有往來已經關閉，無法再傳送訊息。"));
        }

        var message = chatService.SendMessage(chatRoomId, CallerId, content, request.QuotedMessageId);
        await chatHub.Clients.Group(ChatHub.RoomGroup(chatRoomId)).SendAsync("messageReceived", message);

        // 房間群組只有在聊天頁才 join，但未讀徽章在任何分頁都要會動。
        // 所以對「除了自己以外的成員」的個人群組再推一則極小的事件——
        // 收件端不需要知道任何 room id，也不必為此 join 房間。
        var peerIds = chatService.GetRoomMemberIds(chatRoomId)
            .Where(id => id != CallerId)
            .Distinct()
            .ToList();
        if (peerIds.Count > 0)
        {
            await chatHub.Clients
                .Groups(peerIds.Select(ChatHub.UserGroup).ToList())
                .SendAsync("roomActivity", new { roomId = chatRoomId, senderId = CallerId });
        }

        return ApiResponse<ChatMessage>.Ok(message);
    }

    [HttpPost("{chatRoomId}/messages/{messageId}/recall")]
    public async Task<ActionResult<ApiResponse<ChatMessage>>> RecallMessage(string chatRoomId, string messageId)
    {
        if (!IsMember(chatRoomId)) return Forbid();
        var recalled = chatService.RecallMessage(messageId, CallerId);
        if (recalled == null) return BadRequest(ApiResponse<ChatMessage>.Fail("MESSAGE_RECALL_DENIED", "訊息無法收回（僅限本人發出後 2 分鐘內）。"));
        await chatHub.Clients.Group(ChatHub.RoomGroup(chatRoomId)).SendAsync("messageRecalled", recalled);
        return ApiResponse<ChatMessage>.Ok(recalled);
    }

    /// <summary>每間有未讀的房間與其未讀數。沒有未讀的房間不會出現在結果裡。</summary>
    [HttpGet("user/{userId}/unread")]
    public ActionResult<ApiResponse<Dictionary<string, int>>> Unread(string userId)
    {
        if (CallerId != userId) return Forbid();
        return ApiResponse<Dictionary<string, int>>.Ok(chatService.GetUnreadCounts(userId));
    }

    /// <summary>
    /// 把一間房標成已讀。冪等，而且水位只前進不後退，所以重複呼叫安全。
    /// </summary>
    [EnableRateLimiting("write")]
    [HttpPost("{chatRoomId}/read")]
    public ActionResult<ApiResponse<bool>> MarkRead(string chatRoomId)
    {
        if (!IsMember(chatRoomId)) return Forbid();
        return ApiResponse<bool>.Ok(chatService.MarkRoomRead(chatRoomId, CallerId));
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

    // 檢舉對方。送出之後這裡什麼都不改變：房間照常、訊息照傳。
    // 「凍結成唯讀」是後台審核成立之後才發生的事（見 ChatService.GetFrozenRoomIds），
    // 不能綁在送出上——不然任何人都能靠一次誤報把別人的對話鎖死。
    //
    // 也刻意不代為離開：把對話直接弄不見，使用者會以為自己誤觸，
    // 而且檢舉後常常還需要回頭看對方說過什麼。要不要退出由使用者自己決定。
    [HttpPost("{chatRoomId}/report")]
    public ActionResult<ApiResponse<bool>> Report(string chatRoomId, [FromBody] ReportChatRequest? request)
    {
        var other = OtherUserId(chatRoomId);
        if (other == null) return Forbid();
        chatService.ReportUser(CallerId, other, chatRoomId, request?.Reason);
        return ApiResponse<bool>.Ok(true);
    }

    private bool IsMember(string chatRoomId)
        => chatService.IsRoomMember(CallerId, chatRoomId);

    private string? OtherUserId(string chatRoomId)
        => chatService.GetRoomsByUser(CallerId)
            .FirstOrDefault(r => r.Id == chatRoomId)?
            .UserIds.FirstOrDefault(u => u != CallerId);
}

// QuotedSenderName 與 QuotedContent 以前也在這裡，由客戶端自己填，而且三欄之間
// 沒有對帳：quotedMessageId 指向 A、名字寫 B、內容寫一段 B 從來沒說過的話，
// 照樣會被存下來。現在只收 id，其餘由 ChatService.ResolveQuote 去查。
//
// 已經上架的 App 仍然會送那兩欄，不過 System.Text.Json 預設忽略對不上的欄位，
// 舊版本照常能傳訊息，只是引用改由伺服器決定——對它們來說反而是修好的。
public sealed record SendMessageRequest(string Content, string? QuotedMessageId = null);
public sealed record UpdateChatSettingRequest(string? NoteName = null, bool? Pinned = null, bool? Muted = null);
public sealed record ReportChatRequest(string? Reason = null);

public sealed record PartnerQuota(int Used, int Limit, bool Full);
