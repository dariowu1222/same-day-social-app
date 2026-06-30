using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/rants")]
public sealed class RantsController(RantService rantService) : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<List<RantPost>>> Get([FromQuery] string? cursor, [FromQuery] int limit = 20)
    {
        DateTimeOffset? c = DateTimeOffset.TryParse(cursor, out var parsed) ? parsed : null;
        return ApiResponse<List<RantPost>>.Ok(rantService.GetPublicPosts(c, limit));
    }

    [HttpGet("{rantId}")]
    public ActionResult<ApiResponse<RantPost>> GetOne(string rantId)
    {
        var post = rantService.GetPost(rantId);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("RANT_NOT_FOUND", "找不到這篇樹洞文章。"))
            : ApiResponse<RantPost>.Ok(post);
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost]
    public ActionResult<ApiResponse<RantPost>> Create([FromBody] CreateRantRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value!;
        var result = rantService.Create(userId, request.Nickname, request.Content, request.Mode, request.HashTags, request.ImageDataUrls ?? (request.ImageDataUrl is null ? null : [request.ImageDataUrl]), request.AudioDataUrl);
        if (result.Moderation.IsBlocked)
            return BadRequest(ApiResponse<RantPost>.Fail(result.Moderation.Code!, result.Moderation.Message!));
        return ApiResponse<RantPost>.Ok(result.Post!, result.Moderation.Warning);
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost("{rantId}/understand")]
    public ActionResult<ApiResponse<RantPost>> Understand(string rantId)
    {
        var post = rantService.Understand(rantId);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("RANT_NOT_FOUND", "找不到這篇樹洞文章。"))
            : ApiResponse<RantPost>.Ok(post);
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost("{rantId}/replies")]
    public ActionResult<ApiResponse<RantPost>> Reply(string rantId, [FromBody] CreateReplyRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value!;
        var post = rantService.Reply(rantId, userId, request.Nickname, request.Content, request.ImageDataUrl, request.AudioDataUrl, request.ParentReplyId);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("RANT_NOT_FOUND", "找不到這篇樹洞文章。"))
            : ApiResponse<RantPost>.Ok(post);
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
        var post = rantService.LikeReply(rantId, replyId);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("REPLY_NOT_FOUND", "找不到這則回應。"))
            : ApiResponse<RantPost>.Ok(post);
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost("{rantId}/report")]
    public ActionResult<ApiResponse<RantPost>> Report(string rantId)
    {
        var post = rantService.Report(rantId);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("RANT_NOT_FOUND", "找不到這篇樹洞文章。"))
            : ApiResponse<RantPost>.Ok(post);
    }
}

public sealed record CreateRantRequest(string Nickname, string Content, RantMode Mode, List<string>? HashTags = null, List<string>? ImageDataUrls = null, string? ImageDataUrl = null, string? AudioDataUrl = null);
public sealed record CreateReplyRequest(string Nickname, string Content, string? ImageDataUrl = null, string? AudioDataUrl = null, string? ParentReplyId = null);
