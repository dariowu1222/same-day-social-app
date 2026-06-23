namespace SameDaySocialApp.Infrastructure.Media;

/// <summary>
/// 媒體物件儲存抽象。今天的實作是 Supabase Storage；
/// 將來要換 Cloudflare R2 / AWS S3 只需新增 adapter，不動其他程式碼。
/// </summary>
public interface IMediaStorage
{
    bool IsConfigured { get; }

    /// <summary>上傳位元組，回傳可公開存取的 URL。</summary>
    Task<string> UploadAsync(string folder, byte[] bytes, string contentType, CancellationToken ct = default);
}
