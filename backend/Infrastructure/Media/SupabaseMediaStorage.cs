using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace SameDaySocialApp.Infrastructure.Media;

public sealed class SupabaseMediaStorage(IHttpClientFactory httpClientFactory, IOptions<MediaOptions> options) : IMediaStorage
{
    private readonly MediaOptions opt = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(opt.SupabaseUrl)
        && !string.IsNullOrWhiteSpace(opt.ServiceKey)
        && !string.IsNullOrWhiteSpace(opt.Bucket);

    public async Task<string> UploadAsync(string folder, byte[] bytes, string contentType, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("MEDIA_NOT_CONFIGURED");
        }

        var baseUrl = opt.SupabaseUrl.TrimEnd('/');
        var safeFolder = string.IsNullOrWhiteSpace(folder) ? "misc" : folder.Trim('/');
        var path = $"{safeFolder}/{Guid.NewGuid():N}{ExtensionFor(contentType)}";

        var client = httpClientFactory.CreateClient();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        using var req = new HttpRequestMessage(
            HttpMethod.Post, $"{baseUrl}/storage/v1/object/{opt.Bucket}/{path}")
        {
            Content = content,
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.ServiceKey);
        req.Headers.TryAddWithoutValidation("x-upsert", "true");

        var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"MEDIA_UPLOAD_FAILED: {(int)res.StatusCode} {body}");
        }

        // public bucket 的公開讀取 URL
        return $"{baseUrl}/storage/v1/object/public/{opt.Bucket}/{path}";
    }

    private static string ExtensionFor(string contentType) => contentType?.ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        "audio/mpeg" or "audio/mp3" => ".mp3",
        "audio/webm" => ".webm",
        "audio/wav" or "audio/x-wav" => ".wav",
        "audio/mp4" or "audio/m4a" => ".m4a",
        _ => "",
    };
}
