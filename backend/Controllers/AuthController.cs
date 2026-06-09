using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService, JwtService jwtService) : ControllerBase
{
    [HttpPost("demo-login")]
    public ActionResult<ApiResponse<object>> DemoLogin([FromBody] DemoLoginRequest request)
    {
        var user = authService.DemoLogin(request.Nickname);
        var token = jwtService.IssueToken(user.Id, user.Nickname, TimeSpan.FromDays(1));
        return ApiResponse<object>.Ok(new { userId = user.Id, user.Nickname, token });
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<RegistrationStartResponse>>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(
            new RegisterCommand(request.Nickname, request.Email, request.Password, request.ConfirmPassword, request.BirthYear, request.Gender, request.TermsAccepted),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("register/confirm")]
    public async Task<ActionResult<ApiResponse<AuthUserResponse>>> ConfirmRegistration(
        [FromBody] RegistrationConfirmRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.ConfirmRegistrationAsync(new ConfirmRegistrationCommand(request.Email, request.Code), cancellationToken);
        if (!result.Success) return ToActionResult(result);
        var token = jwtService.IssueToken(result.Data!.UserId, result.Data.Nickname);
        return ApiResponse<AuthUserResponse>.Ok(result.Data with { Token = token });
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthUserResponse>>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(new LoginCommand(request.Email, request.Password), cancellationToken);
        if (!result.Success) return ToActionResult(result);
        var token = jwtService.IssueToken(result.Data!.UserId, result.Data.Nickname);
        return ApiResponse<AuthUserResponse>.Ok(result.Data with { Token = token });
    }

    [HttpPost("password-reset/request")]
    public async Task<ActionResult<ApiResponse<PasswordResetRequestResponse>>> RequestPasswordReset(
        [FromBody] PasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RequestPasswordResetAsync(request.Email, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("password-reset/verify")]
    public async Task<ActionResult<ApiResponse<PasswordResetVerifyResponse>>> VerifyPasswordReset(
        [FromBody] PasswordResetVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.VerifyPasswordResetCodeAsync(request.Email, request.Code, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("password-reset/confirm")]
    public async Task<ActionResult<ApiResponse<AuthUserResponse>>> ConfirmPasswordReset(
        [FromBody] PasswordResetConfirmRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.ConfirmPasswordResetAsync(
            new ConfirmPasswordResetCommand(request.Email, request.Code, request.NewPassword, request.ConfirmPassword),
            cancellationToken);
        if (!result.Success) return ToActionResult(result);
        var token = jwtService.IssueToken(result.Data!.UserId, result.Data.Nickname);
        return ApiResponse<AuthUserResponse>.Ok(result.Data with { Token = token });
    }

    private ActionResult<ApiResponse<T>> ToActionResult<T>(AuthServiceResult<T> result)
    {
        if (result.Success) return ApiResponse<T>.Ok(result.Data!);
        return BadRequest(ApiResponse<T>.Fail(result.Code!, result.Message!));
    }
}

public sealed record DemoLoginRequest(string Nickname);
public sealed record RegisterRequest(string Nickname, string Email, string Password, string ConfirmPassword, string BirthYear, string Gender, bool TermsAccepted);
public sealed record RegistrationConfirmRequest(string Email, string Code);
public sealed record LoginRequest(string Email, string Password);
public sealed record PasswordResetRequest(string Email);
public sealed record PasswordResetVerifyRequest(string Email, string Code);
public sealed record PasswordResetConfirmRequest(string Email, string Code, string NewPassword, string ConfirmPassword);
