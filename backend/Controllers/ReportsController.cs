using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Controllers;

/// <summary>
/// 使用者端檢舉。統一入口，樹洞貼文／回應／聊天訊息／使用者都走這裡。
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    private string CallerId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value!;

    [EnableRateLimiting("write")]
    [HttpPost]
    public ActionResult<ApiResponse<SubmitReportResponse>> Submit([FromBody] SubmitReportRequest request)
    {
        var result = reports.Submit(
            CallerId, request.TargetType, request.TargetId, request.Category, request.Detail);

        if (result.Error != null)
        {
            var message = result.Error switch
            {
                "REPORT_TARGET_NOT_FOUND" => "找不到要檢舉的內容，可能已被刪除。",
                "REPORT_SELF" => "無法檢舉自己的內容。",
                "REPORT_UNAVAILABLE" => "檢舉服務暫時無法使用，請稍後再試。",
                _ => "檢舉資料不正確。",
            };
            return BadRequest(ApiResponse<SubmitReportResponse>.Fail(result.Error, message));
        }

        // 自我傷害的檢舉不談「違規處理」，回覆聚焦在關懷與求助資源
        var isSelfHarm = request.Category == ReportCategory.SELF_HARM;
        var note = isSelfHarm
            ? "謝謝你願意留意身邊的人。我們會盡快關心對方。"
            : result.AlreadyReported
                ? "你已經檢舉過這則內容，我們正在處理。"
                : "已收到你的檢舉，我們會盡快審核。";

        return ApiResponse<SubmitReportResponse>.Ok(
            new SubmitReportResponse(result.Report?.Id ?? "", result.AlreadyReported, isSelfHarm, note));
    }
}

public sealed record SubmitReportRequest(
    ReportTargetType TargetType,
    string TargetId,
    ReportCategory Category,
    string? Detail = null);

public sealed record SubmitReportResponse(
    string ReportId,
    bool AlreadyReported,
    bool SelfHarmSupport,
    string Message);
