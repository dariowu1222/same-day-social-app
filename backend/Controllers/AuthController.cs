using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("demo-login")]
    public ActionResult<ApiResponse<object>> DemoLogin([FromBody] DemoLoginRequest request)
    {
        var user = authService.DemoLogin(request.Nickname);
        return ApiResponse<object>.Ok(new { userId = user.Id, user.Nickname });
    }
}

public sealed record DemoLoginRequest(string Nickname);
