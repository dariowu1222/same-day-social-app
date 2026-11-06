using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Application.Validation;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Media;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/rants")]
public sealed class RantsController(RantService rantService, MediaUrlPolicy mediaUrls) : ControllerBase
{
    // rant_posts.content 是 varchar(3000)、rant_replies.content 是 varchar(1000)。
    // 超過就直接撞上 SaveChanges 丟 Npgsql 例外變成 500，使用者只看到「伺服器錯誤」。
    private const int MaxPostLength = 3000;
    private const int MaxReplyLength = 1000;

    [HttpGet]
    public ActionResult<ApiResponse<List<RantPost>>> Get([FromQuery] string? cursor, [FromQuery] int limit = 20)
    {
        DateTimeOffset? c = DateTimeOffset.TryParse(cursor, out var parsed) ? parsed : null;
        return ApiResponse<List<RantPost>>.Ok([.. rantService.GetPublicPosts(c, limit).Select(Mine)]);
    }

    [HttpGet("{rantId}")]
    public ActionResult<ApiResponse<RantPost>> GetOne(string rantId)
    {
        var post = rantService.GetPost(rantId);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("RANT_NOT_FOUND", "找不到這篇樹洞文章。"))
            : ApiResponse<RantPost>.Ok(Mine(post));
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost]
    public ActionResult<ApiResponse<RantPost>> Create([FromBody] CreateRantRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value!;

        // Content 宣告為不可為 null，但 JSON 送 null 一樣會進來，
        // 下游的 content.Trim() 會 NRE 變成 500。
        var content = request.Content ?? "";
        if (content.Length > MaxPostLength)
        {
            return BadRequest(ApiResponse<RantPost>.Fail("FIELD_TOO_LONG", $"內容最多 {MaxPostLength} 字。"));
        }
        if (!InputLimits.WithinList(request.HashTags, InputLimits.MaxTagCount, InputLimits.MaxTagLength))
        {
            return BadRequest(ApiResponse<RantPost>.Fail(
                "FIELD_TOO_LONG", $"標籤最多 {InputLimits.MaxTagCount} 個，每個最多 {InputLimits.MaxTagLength} 字。"));
        }

        var images = request.ImageUrls ?? (request.ImageUrl is null ? null : [request.ImageUrl]);
        if (!MediaIsOurs([.. images ?? [], .. request.ThumbUrls ?? [], request.AudioUrl]))
        {
            return BadRequest(ApiResponse<RantPost>.Fail("INVALID_MEDIA_URL", "圖片或語音必須是從這個 App 上傳的。"));
        }

        var result = rantService.Create(userId, content, request.Mode, request.HashTags, images, request.AudioUrl, request.ThumbUrls);
        if (result.Moderation.IsBlocked)
            return BadRequest(ApiResponse<RantPost>.Fail(result.Moderation.Code!, result.Moderation.Message!));
        return ApiResponse<RantPost>.Ok(Mine(result.Post!), result.Moderation.Warning);
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost("{rantId}/understand")]
    public ActionResult<ApiResponse<RantPost>> Understand(string rantId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value!;
        var post = rantService.Understand(rantId, userId);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("RANT_NOT_FOUND", "找不到這篇樹洞文章。"))
            : ApiResponse<RantPost>.Ok(Mine(post));
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost("{rantId}/replies")]
    public ActionResult<ApiResponse<RantPost>> Reply(string rantId, [FromBody] CreateReplyRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value!;

        var content = request.Content ?? "";
        if (content.Length > MaxReplyLength)
        {
            return BadRequest(ApiResponse<RantPost>.Fail("FIELD_TOO_LONG", $"回覆最多 {MaxReplyLength} 字。"));
        }
        if (!MediaIsOurs([request.ImageUrl, request.ThumbUrl, request.AudioUrl]))
        {
            return BadRequest(ApiResponse<RantPost>.Fail("INVALID_MEDIA_URL", "圖片或語音必須是從這個 App 上傳的。"));
        }

        var post = rantService.Reply(rantId, userId, content, request.ImageUrl, request.AudioUrl, request.ParentReplyId, request.ThumbUrl);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("RANT_NOT_FOUND", "找不到這篇樹洞文章。"))
            : ApiResponse<RantPost>.Ok(Mine(post));
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [HttpDelete("{rantId}")]
    public ActionResult<ApiResponse<bool>> Delete(string rantId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value!;
        var deleted = rantService.Delete(rantId, userId);
        return deleted
            ? ApiResponse<bool>.Ok(true)
            : NotFound(ApiResponse<bool>.Fail("RANT_NOT_FOUND", "找不到貼文，或你沒有權限刪除。"));
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost("{rantId}/replies/{replyId}/understand")]
    public ActionResult<ApiResponse<RantPost>> LikeReply(string rantId, string replyId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value!;
        var post = rantService.LikeReply(rantId, replyId, userId);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("REPLY_NOT_FOUND", "找不到這則回應。"))
            : ApiResponse<RantPost>.Ok(Mine(post));
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost("{rantId}/report")]
    public ActionResult<ApiResponse<RantPost>> Report(string rantId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value!;
        var post = rantService.Report(rantId, userId);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("RANT_NOT_FOUND", "找不到這篇樹洞文章。"))
            : ApiResponse<RantPost>.Ok(Mine(post));
    }

    /// <summary>
    /// 標上「這篇／這則是不是我發的」。
    ///
    /// 作者的 user id 不再送出去（RantPost.UserId 掛了 JsonIgnore）——
    /// 這兩支 GET 沒有 [Authorize]，任何人都讀得到，帶著 user id 等於
    /// 匿名發文可以被對應回某一個帳號，再拿去 /api/profile/{userId} 查出是誰。
    ///
    /// 前端要的只是「要不要顯示刪除選單」，這個布林值就夠。未登入時
    /// CallerId 是 null，IsMine 一律 false。
    /// </summary>
    private RantPost Mine(RantPost post)
    {
        var callerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(callerId))
        {
            return post;
        }

        post.IsMine = post.UserId == callerId;
        MarkReplies(post.Replies);
        return post;

        void MarkReplies(List<RantReply> replies)
        {
            foreach (var reply in replies)
            {
                reply.IsMine = reply.UserId == callerId;
                MarkReplies(reply.Replies);
            }
        }
    }

    /// <summary>
    /// 圖片、縮圖、語音都必須是自家物件儲存發出來的。
    ///
    /// 樹洞是公開牆，一篇貼文會出現在所有人的畫面上。網址放自己的伺服器，
    /// 就等於在每個看過這篇的人身上裝一個追蹤像素——IP、裝置、看的時間全都有了。
    /// 換成假的系統提示圖片就是釣魚。
    ///
    /// null 與空字串是「這則沒有附媒體」，照常放行。
    /// </summary>
    private bool MediaIsOurs(IEnumerable<string?> urls)
        => urls.Where(u => !string.IsNullOrWhiteSpace(u)).All(mediaUrls.IsOwn);
}

// 這兩個 record 以前的第一個欄位是 Nickname，由客戶端自己填。userId 取自 JWT、
// 暱稱取自 request body，兩者從來沒有被對帳過，等於任何人都能用任意顯示名發文。
// 現在暱稱由 RantService.CurrentNickname 去 users 查，這裡不再收。
//
// 已經上架的 App 仍然會送 nickname，不過 System.Text.Json 預設忽略對不上的欄位，
// 舊版本照常能發文，只是名字改由伺服器決定——對它們來說反而是修好的。
public sealed record CreateRantRequest(string Content, RantMode Mode, List<string>? HashTags = null, List<string>? ImageUrls = null, string? ImageUrl = null, string? AudioUrl = null, List<string>? ThumbUrls = null);
public sealed record CreateReplyRequest(string Content, string? ImageUrl = null, string? AudioUrl = null, string? ParentReplyId = null, string? ThumbUrl = null);
