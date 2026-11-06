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

    public string PublicUrlPrefix =>
        IsConfigured ? $"{opt.SupabaseUrl.TrimEnd('/')}/storage/v1/object/public/{opt.Bucket}/" : "";

    public async Task<string> UploadAsync(MediaFolder folder, byte[] bytes, string contentType, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("MEDIA_NOT_CONFIGURED");
        }

        var extension = MediaContentTypes.ExtensionFor(contentType)
                        ?? throw new InvalidOperationException("MEDIA_TYPE_NOT_ALLOWED");

        var baseUrl = opt.SupabaseUrl.TrimEnd('/');

        // 路徑三段全部由伺服器決定：白名單前綴、新的 GUID、白名單副檔名。
        // GUID 的 "N" 格式只有十六進位字元，三段合起來不含 /、#、?、. 以外的東西，
        // 所以不可能跳出 bucket，也不可能撞到既有的檔案。
        //
        // 刻意不把 userId 放進路徑：這個網址是公開的，而樹洞是匿名發文的地方，
        // 路徑裡有 user id 等於每張圖都附上作者是誰。
        var path = $"{MediaFolders.PrefixOf(folder)}/{Guid.NewGuid():N}{extension}";

        var client = httpClientFactory.CreateClient();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        // 不帶 x-upsert：路徑是剛產生的 GUID，沒有既有物件要覆寫。
        // 留著的話，任何一個讓路徑可被影響的新漏洞都會從「多一個檔案」
        // 直接升級成「換掉別人的檔案」。
        using var req = new HttpRequestMessage(
            HttpMethod.Post, $"{baseUrl}/storage/v1/object/{opt.Bucket}/{path}")
        {
            Content = content,
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.ServiceKey);

        var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"MEDIA_UPLOAD_FAILED: {(int)res.StatusCode} {body}");
        }

        // public bucket 的公開讀取 URL
        return $"{PublicUrlPrefix}{path}";
    }
}
