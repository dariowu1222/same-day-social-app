using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Infrastructure.Media;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/media")]
[Authorize]
public sealed class MediaController(IMediaStorage media) : ControllerBase
{
    // 接前端縮圖後的 data URL → 上傳物件儲存 → 回公開 URL。
    // 物件儲存未設定（dev）時原樣回傳 data URL，維持可用、不破壞既有流程。
    [HttpPost]
    public async Task<ActionResult<ApiResponse<MediaUploadResponse>>> Upload(
        [FromBody] MediaUploadRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DataUrl))
        {
            return BadRequest(ApiResponse<MediaUploadResponse>.Fail("MEDIA_EMPTY", "沒有收到媒體內容。"));
        }

        if (!media.IsConfigured)
        {
            return ApiResponse<MediaUploadResponse>.Ok(new MediaUploadResponse(request.DataUrl));
        }

        var parsed = ParseDataUrl(request.DataUrl);
        if (parsed == null)
        {
            return BadRequest(ApiResponse<MediaUploadResponse>.Fail("MEDIA_INVALID", "媒體格式不正確。"));
        }

        try
        {
            var url = await media.UploadAsync(request.Folder ?? "misc", parsed.Value.Bytes, parsed.Value.ContentType, ct);
            return ApiResponse<MediaUploadResponse>.Ok(new MediaUploadResponse(url));
        }
        catch (Exception)
        {
            return StatusCode(502, ApiResponse<MediaUploadResponse>.Fail("MEDIA_UPLOAD_FAILED", "媒體上傳失敗，請稍後再試。"));
        }
    }

    private static (byte[] Bytes, string ContentType)? ParseDataUrl(string dataUrl)
    {
        var m = Regex.Match(dataUrl, @"^data:(?<type>[^;]+);base64,(?<data>.+)$", RegexOptions.Singleline);
        if (!m.Success)
        {
            return null;
        }
        try
        {
            return (Convert.FromBase64String(m.Groups["data"].Value), m.Groups["type"].Value);
        }
        catch
        {
            return null;
        }
    }
}

public sealed record MediaUploadRequest(string DataUrl, string? Folder = null);
public sealed record MediaUploadResponse(string Url);
