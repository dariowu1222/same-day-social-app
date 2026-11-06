namespace SameDaySocialApp.Infrastructure.Media;

/// <summary>
/// 媒體物件儲存抽象。今天的實作是 Supabase Storage；
/// 將來要換 Cloudflare R2 / AWS S3 只需新增 adapter，不動其他程式碼。
/// </summary>
public interface IMediaStorage
{
    bool IsConfigured { get; }

    /// <summary>
    /// 這個儲存體發出來的公開網址前綴（結尾含 /）。未設定時為空字串。
    /// 寫入端用它判斷一個網址是不是自家的（見 MediaUrlPolicy）。
    /// </summary>
    string PublicUrlPrefix { get; }

    /// <summary>
    /// 上傳位元組，回傳可公開存取的 URL。
    /// 路徑完全由伺服器決定（白名單前綴 + 新的 GUID），呼叫端無法影響。
    /// </summary>
    Task<string> UploadAsync(MediaFolder folder, byte[] bytes, string contentType, CancellationToken ct = default);
}
