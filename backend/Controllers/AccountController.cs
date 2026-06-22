using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController(AuthService authService) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    // 刪除帳號（軟刪除：停用登入並清個資）
    [HttpDelete]
    public ActionResult<ApiResponse<bool>> Delete()
    {
        var ok = authService.DeleteAccount(CallerId);
        return ok
            ? ApiResponse<bool>.Ok(true)
            : NotFound(ApiResponse<bool>.Fail("ACCOUNT_NOT_FOUND", "找不到帳號。"));
    }
}
