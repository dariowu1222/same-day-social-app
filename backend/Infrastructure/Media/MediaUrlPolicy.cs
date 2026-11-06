using System.Text.RegularExpressions;

namespace SameDaySocialApp.Infrastructure.Media;

/// <summary>
/// 哪些網址可以被存成使用者內容。
///
/// 為什麼需要這條：頭像、發文圖片、語音、聊天訊息都接受任意 https 網址，而前端
/// 看到像媒體的字串就直接 &lt;img src&gt; / &lt;audio src&gt; 下去。於是任何人都能
/// 送一則「訊息」，內容是自己伺服器上的 1×1 圖片——對方一打開對話，瀏覽器就替
/// 攻擊者送出一次請求，IP、裝置、確切的上線時間全都有了。同一招換成假的系統提示
/// 圖片就是釣魚。
///
/// 規則：會被自動載入的東西必須來自自家的物件儲存。純文字的網址不受影響——
/// 那是使用者自己要不要點的事，而且訊息裡貼連結本來就是正常用法。
/// </summary>
public sealed class MediaUrlPolicy(IMediaStorage storage)
{
    /// <summary>
    /// 自家儲存體發出來的網址長度是固定的一百出頭（前綴 + 前綴目錄 + 32 字 GUID + 副檔名）。
    /// 500 是留給將來換儲存體的餘裕，不是什麼需要精算的數字。
    /// </summary>
    public const int MaxUrlLength = 500;

    // 與前端 ChatRoom.isImageMedia / ChatPage.isImagePreview 同一組規則。
    // 那兩處是「會不會被渲染成媒體」的實際判準，這裡要蓋住的正是同一個集合，
    // 所以改了任何一邊，三處都要一起改。
    private static readonly Regex ImageLike = new(
        @"^data:image/|^https?://\S*\.(jpe?g|png|webp|gif)(\?|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AudioLike = new(
        @"^data:audio/|^https?://\S*\.(mp3|webm|wav|m4a|ogg)(\?|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>客戶端會不會把這串東西自動載入成圖片或語音。</summary>
    public static bool RendersAsMedia(string? content)
    {
        var value = content?.Trim();
        return !string.IsNullOrEmpty(value) && (ImageLike.IsMatch(value) || AudioLike.IsMatch(value));
    }

    /// <summary>
    /// 這個網址是不是自家物件儲存發出來的。
    ///
    /// 本機開發沒接 Supabase 時（MediaController 會原樣退回 data URL）才放行 data:，
    /// 正式環境一律不收——那條路正是 chat_messages 裡那幾則整串 base64 的來源。
    /// </summary>
    public bool IsOwn(string? url)
    {
        var value = url?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > MaxUrlLength)
        {
            return false;
        }

        if (!storage.IsConfigured)
        {
            return value.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
        }

        return value.StartsWith(storage.PublicUrlPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// 會被自動載入、但不是自家的 → 擋下。
    /// 純文字（含使用者自己貼的一般連結）回 true，不受影響。
    /// </summary>
    public bool IsAllowedAsMessage(string? content) => !RendersAsMedia(content) || IsOwn(content);
}
