using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;

namespace SameDaySocialApp.Infrastructure.Security;

/// <summary>
/// 後台端點的守門。掛上去之後，一個請求要通過三關才會進到 action：
///
///   1. token 的 scope 必須是 admin —— 一般 App token 到這裡就結束了，回 404。
///      這是整套設計的關鍵：管理者手機上那顆七天有效的 App token 外流，
///      也打不開後台的任何一扇門。
///   2. admin_sessions 裡那一列要還活著 —— 沒撤銷、沒過期、沒閒置超時。
///   3. 使用者此刻仍在白名單上 —— 名單是在後端啟動時讀的，但人被移除之後
///      舊 session 不該還能用。
///
/// 第 1 關失敗回 404（不揭露端點存在），第 2 關失敗回 401 並帶
/// ADMIN_SESSION_EXPIRED，讓後台知道該重新要一次驗證碼而不是當成壞掉。
///
/// 做成 filter 而不是在每個 action 開頭呼叫：驗證是非同步的（要查 DB），
/// 而現有的後台 action 全是同步方法。在同步方法裡 .Wait() 一個非同步呼叫，
/// 正是會把執行緒池吃光的那種寫法。
/// </summary>
public sealed class AdminSessionFilter(AdminSessionService sessions, AdminAccessService admin)
    : IAsyncAuthorizationFilter
{
    /// <summary>通過驗證後，這次請求的「驗證碼是否夠新」會放在 HttpContext.Items 的這個鍵底下。</summary>
    public const string RecentlyVerifiedKey = "AdminRecentlyVerified";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? user.FindFirst("sub")?.Value;
        var scope = user.FindFirst(JwtService.AdminScopeClaim)?.Value;
        var sessionId = user.FindFirst(JwtService.AdminSessionClaim)?.Value;

        if (string.IsNullOrWhiteSpace(userId)
            || scope != JwtService.AdminScopeValue
            || string.IsNullOrWhiteSpace(sessionId)
            || !admin.IsAdmin(userId))
        {
            context.Result = new NotFoundObjectResult(
                ApiResponse<object>.Fail("NOT_FOUND", "找不到此資源。"));
            return;
        }

        var state = await sessions.TouchAsync(userId, sessionId, context.HttpContext.RequestAborted);
        if (!state.Valid)
        {
            context.Result = new ObjectResult(
                ApiResponse<object>.Fail("ADMIN_SESSION_EXPIRED", "後台登入已過期，請重新驗證。"))
            {
                StatusCode = StatusCodes.Status401Unauthorized,
            };
            return;
        }

        context.HttpContext.Items[RecentlyVerifiedKey] = state.RecentlyVerified;
    }
}
