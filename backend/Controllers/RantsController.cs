using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/rants")]
public sealed class RantsController(RantService rantService) : ControllerBase
{
    [HttpPost]
    public ActionResult<ApiResponse<RantPost>> Create([FromBody] CreateRantRequest request)
    {
        var result = rantService.Create(request.UserId, request.Nickname, request.Content, request.Mode);
        if (result.Moderation.IsBlocked)
        {
            return BadRequest(ApiResponse<RantPost>.Fail(result.Moderation.Code!, result.Moderation.Message!));
        }

        return ApiResponse<RantPost>.Ok(result.Post!, result.Moderation.Warning);
    }

    [HttpGet]
    public ActionResult<ApiResponse<List<RantPost>>> Get([FromQuery] string? userId = null)
    {
        return ApiResponse<List<RantPost>>.Ok(rantService.GetPublicPosts(userId));
    }

    [HttpPost("{rantId}/understand")]
    public ActionResult<ApiResponse<RantPost>> Understand(string rantId, [FromBody] UnderstandRequest request)
    {
        var post = rantService.Understand(rantId, request.UserId);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("RANT_NOT_FOUND", "找不到這篇樹洞文章。"))
            : ApiResponse<RantPost>.Ok(post);
    }

    [HttpPost("{rantId}/replies")]
    public ActionResult<ApiResponse<RantPost>> Reply(string rantId, [FromBody] CreateReplyRequest request)
    {
        var post = rantService.Reply(rantId, request.UserId, request.Nickname, request.Content, request.ParentReplyId);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("RANT_NOT_FOUND", "找不到這篇樹洞文章。"))
            : ApiResponse<RantPost>.Ok(post);
    }

    [HttpPost("{rantId}/report")]
    public ActionResult<ApiResponse<RantPost>> Report(string rantId)
    {
        var post = rantService.Report(rantId);
        return post == null
            ? NotFound(ApiResponse<RantPost>.Fail("RANT_NOT_FOUND", "找不到這篇樹洞文章。"))
            : ApiResponse<RantPost>.Ok(post);
    }
}

public sealed record CreateRantRequest(string UserId, string Nickname, string Content, RantMode Mode);
public sealed record CreateReplyRequest(string UserId, string Nickname, string Content, string? ParentReplyId = null);
public sealed record UnderstandRequest(string UserId);
