namespace SameDaySocialApp.Infrastructure.Media;

/// <summary>
/// 允許的上傳位置。
///
/// 為什麼是 enum 而不是字串：這個值最後會變成物件儲存裡的路徑，而上傳用的是
/// service_role 金鑰、還帶 x-upsert。原本 folder 由客戶端自由填寫，只做了
/// Trim('/')：
///   - 送 "avatars/某人的檔名.jpg#"，後面伺服器產生的 GUID 會被當成 fragment
///     丟掉，請求實際打到那個人的檔案上，直接覆寫。
///   - 送 "../"，.NET 的 Uri 會正規化掉，於是跳出 bucket——而 service_role
///     對同一個 Supabase 專案的所有 bucket 都有權限。
///
/// 改成 enum 之後，呼叫端能表達的只有下面這幾個值，拼不出別的路徑。
/// </summary>
public enum MediaFolder
{
    Avatars,
    AvatarThumbs,
    Chats,
    ChatThumbs,
    Rants,
    RantThumbs,
}

public static class MediaFolders
{
    /// <summary>
    /// 客戶端送上來的名字 → 位置。這份對照表就是白名單：不在表上的一律拒絕，
    /// 不退回「misc」之類的預設值——悄悄收下一個看不懂的位置，等於把判斷權還給呼叫端。
    ///
    /// 這六個名字是前端歷來用過的全部（avatars / chats / rants，各自再加 /thumbs，
    /// 由 uploadImageWithThumb 組出來），已上架的版本不會送別的。
    /// </summary>
    private static readonly Dictionary<string, MediaFolder> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["avatars"] = MediaFolder.Avatars,
        ["avatars/thumbs"] = MediaFolder.AvatarThumbs,
        ["chats"] = MediaFolder.Chats,
        ["chats/thumbs"] = MediaFolder.ChatThumbs,
        ["rants"] = MediaFolder.Rants,
        ["rants/thumbs"] = MediaFolder.RantThumbs,
    };

    public static MediaFolder? Parse(string? folder)
        => folder != null && ByName.TryGetValue(folder.Trim(), out var parsed) ? parsed : null;

    /// <summary>物件儲存裡的路徑前綴。只會是下面這六個常數之一。</summary>
    public static string PrefixOf(MediaFolder folder) => folder switch
    {
        MediaFolder.Avatars => "avatars",
        MediaFolder.AvatarThumbs => "avatars/thumbs",
        MediaFolder.Chats => "chats",
        MediaFolder.ChatThumbs => "chats/thumbs",
        MediaFolder.Rants => "rants",
        MediaFolder.RantThumbs => "rants/thumbs",
        _ => throw new ArgumentOutOfRangeException(nameof(folder), folder, "未知的上傳位置"),
    };
}
