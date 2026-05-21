using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Entities;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TasksController(TaskService taskService) : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<List<SocialTask>>> Get()
    {
        return ApiResponse<List<SocialTask>>.Ok(taskService.GetTasks());
    }

    [HttpPost("{taskId}/join")]
    public ActionResult<ApiResponse<SocialTask>> Join(string taskId, [FromBody] JoinTaskRequest request)
    {
        var task = taskService.Join(taskId, request.UserId);
        return task == null ? NotFound(ApiResponse<SocialTask>.Fail("TASK_NOT_FOUND", "找不到任務。")) : ApiResponse<SocialTask>.Ok(task);
    }
}

public sealed record JoinTaskRequest(string UserId);
