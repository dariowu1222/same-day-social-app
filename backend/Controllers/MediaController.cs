using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Infrastructure.Media;

namespace SameDaySocialApp.Controllers;

[ApiController]
[Route("api/media")]
[Authorize]
public sealed class MediaController(IMediaStorage media, IWebHostEnvironment environment) : ControllerBase
{
    // 接前端縮圖後的 data URL → 上傳物件儲存 → 回公開 URL。
    //
    // 全域的請求大小上限是 1 MB（Program.cs），這支是唯一需要放寬的端點：
    // 5 MB 的圖片編成 base64 大約 6.7 MB，加上 JSON 外框抓 12 MB。
    // 解碼之後還會再按型別各自卡一次（圖 5 MB、語音 2 MB）。
    [EnableRateLimiting("write")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<MediaUploadResponse>>> Upload(
        [FromBody] MediaUploadRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DataUrl))
        {
            return BadRequest(ApiResponse<MediaUploadResponse>.Fail("MEDIA_EMPTY", "沒有收到媒體內容。"));
        }

        // 物件儲存沒設定時原樣退回 data URL，讓本機開發不必先接 Supabase。
        //
        // 這條以前在正式環境也成立，而前端拿到 data URL 就直接存進資料庫：
        // chat_messages 現在就有三則整串 base64 的訊息，最長 120 KB。
        // 正式環境改成直接回錯——沒接好儲存體是設定問題，不該用灌爆資料庫來掩蓋。
        if (!media.IsConfigured)
        {
            return environment.IsDevelopment()
                ? ApiResponse<MediaUploadResponse>.Ok(new MediaUploadResponse(request.DataUrl))
                : StatusCode(503, ApiResponse<MediaUploadResponse>.Fail(
                    "MEDIA_NOT_CONFIGURED", "媒體儲存尚未設定，目前無法上傳。"));
        }

        var folder = MediaFolders.Parse(request.Folder);
        if (folder == null)
        {
            return BadRequest(ApiResponse<MediaUploadResponse>.Fail("MEDIA_FOLDER_INVALID", "上傳位置不正確。"));
        }

        var parsed = ParseDataUrl(request.DataUrl);
        if (parsed == null)
        {
            return BadRequest(ApiResponse<MediaUploadResponse>.Fail("MEDIA_INVALID", "媒體格式不正確。"));
        }

        var (bytes, contentType) = parsed.Value;

        if (MediaContentTypes.ExtensionFor(contentType) == null)
        {
            return BadRequest(ApiResponse<MediaUploadResponse>.Fail("MEDIA_TYPE_NOT_ALLOWED", "不支援這種檔案類型。"));
        }

        if (bytes.Length > MediaContentTypes.MaxBytesFor(contentType))
        {
            return BadRequest(ApiResponse<MediaUploadResponse>.Fail("MEDIA_TOO_LARGE", "檔案太大了，請換一個小一點的。"));
        }

        if (!MediaContentTypes.MatchesMagicBytes(contentType, bytes))
        {
            return BadRequest(ApiResponse<MediaUploadResponse>.Fail("MEDIA_TYPE_MISMATCH", "檔案內容與類型不符。"));
        }

        try
        {
            var url = await media.UploadAsync(folder.Value, bytes, contentType, ct);
            return ApiResponse<MediaUploadResponse>.Ok(new MediaUploadResponse(url));
        }
        catch (Exception)
        {
            return StatusCode(502, ApiResponse<MediaUploadResponse>.Fail("MEDIA_UPLOAD_FAILED", "媒體上傳失敗，請稍後再試。"));
        }
    }

    /// <summary>
    /// data:[&lt;type&gt;][;參數]*;base64,&lt;內容&gt;
    ///
    /// 以前這裡是 ^data:(?&lt;type&gt;[^;]+);base64, 的正規表示式，中間不容許任何參數。
    /// iOS 的 WKWebView 錄音出來是 data:audio/mp4;codecs=mp4a.40.2;base64,...，
    /// 比對不到就回 400，而前端的 uploadMedia 把錯誤吞掉、退回原本的 data URL，
    /// 於是整串 base64 被寫進資料庫。iPhone 使用者的語音訊息一路都是這樣存的。
    /// </summary>
    private static (byte[] Bytes, string ContentType)? ParseDataUrl(string dataUrl)
    {
        if (!dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var comma = dataUrl.IndexOf(',');
        if (comma < 0)
        {
            return null;
        }

        var header = dataUrl[5..comma].Split(';');
        if (header.Length < 2 || !header[^1].Trim().Equals("base64", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var contentType = header[0].Trim().ToLowerInvariant();
        if (contentType.Length == 0)
        {
            return null;
        }

        try
        {
            return (Convert.FromBase64String(dataUrl[(comma + 1)..]), contentType);
        }
        catch
        {
            return null;
        }
    }
}

public sealed record MediaUploadRequest(string DataUrl, string? Folder = null);
public sealed record MediaUploadResponse(string Url);
