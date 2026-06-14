using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public sealed class TasksController(TaskService taskService) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    [HttpGet]
    public ActionResult<ApiResponse<List<SocialTask>>> Get()
    {
        return ApiResponse<List<SocialTask>>.Ok(taskService.GetTasks());
    }

    [HttpPost("{taskId}/join")]
    public ActionResult<ApiResponse<SocialTask>> Join(string taskId, [FromBody] JoinTaskRequest request)
    {
        // 以 token 的身分加入，忽略 body 傳來的 userId，避免冒名加入。
        _ = request;
        var task = taskService.Join(taskId, CallerId);
        return task == null
            ? NotFound(ApiResponse<SocialTask>.Fail("TASK_NOT_FOUND", "找不到這個任務。"))
            : ApiResponse<SocialTask>.Ok(task);
    }
}

public sealed record JoinTaskRequest(string UserId);
