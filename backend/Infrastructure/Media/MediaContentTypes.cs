using System.Text;

namespace SameDaySocialApp.Infrastructure.Media;

/// <summary>
/// 允許上傳的媒體型別，以及「檔案開頭長得像不像它宣告的型別」。
///
/// 為什麼需要白名單：content-type 原本直接採用 data URL 裡宣告的那一段，
/// 原樣送給 Supabase。宣告成 text/html 或 image/svg+xml 就會得到一個掛在
/// supabase.co 網域、可公開存取的網頁——拿去釣魚時網址看起來是自己人的。
///
/// 為什麼還要看檔案開頭：宣告是免費的。白名單只擋住「老實說自己是 HTML」的人，
/// 把同一份 HTML 標成 image/png 照樣會過。檔頭對不上就拒絕，上傳的東西才真的
/// 是它說的那種檔案。
///
/// 清單以前端實際會產生的為準：圖片一律是 canvas 轉出來的 JPEG
/// （mediaTools.imageToResizedDataUrl），語音是 MediaRecorder 的原生格式——
/// Android 是 webm/opus，iOS 的 WKWebView 是 mp4/aac。PNG、WebP、GIF、MP3、WAV
/// 留著是因為聊天室的圖片走的是使用者挑的原檔。
/// </summary>
public static class MediaContentTypes
{
    /// <summary>圖片上限 5 MB、語音上限 2 MB（解碼之後的實際位元組）。</summary>
    public const int MaxImageBytes = 5 * 1024 * 1024;
    public const int MaxAudioBytes = 2 * 1024 * 1024;

    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
        ["audio/webm"] = ".webm",
        ["audio/mp4"] = ".m4a",
        ["audio/m4a"] = ".m4a",
        ["audio/mpeg"] = ".mp3",
        ["audio/mp3"] = ".mp3",
        ["audio/wav"] = ".wav",
        ["audio/x-wav"] = ".wav",
    };

    /// <summary>白名單內才回副檔名；不在清單上回 null，呼叫端據此拒絕。</summary>
    public static string? ExtensionFor(string contentType)
        => Allowed.TryGetValue(contentType.Trim(), out var ext) ? ext : null;

    public static bool IsAudio(string contentType)
        => contentType.Trim().StartsWith("audio/", StringComparison.OrdinalIgnoreCase);

    public static int MaxBytesFor(string contentType)
        => IsAudio(contentType) ? MaxAudioBytes : MaxImageBytes;

    /// <summary>
    /// 檔案開頭是不是它宣告的那種格式。
    ///
    /// 容器格式（webm / mp4）兩邊通用：webm 是 Matroska 的 EBML 檔頭，mp4 是
    /// 第 5 到 8 個位元組的 "ftyp"，兩者都不區分裡面是影片還是只有音軌，
    /// 所以這裡只認容器，不保證編碼。擋掉 HTML/SVG 這個目的已經達成。
    /// </summary>
    public static bool MatchesMagicBytes(string contentType, ReadOnlySpan<byte> bytes) =>
        contentType.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => StartsWith(bytes, [0xFF, 0xD8, 0xFF]),
            "image/png" => StartsWith(bytes, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            "image/webp" => HasAscii(bytes, 0, "RIFF") && HasAscii(bytes, 8, "WEBP"),
            "image/gif" => HasAscii(bytes, 0, "GIF87a") || HasAscii(bytes, 0, "GIF89a"),
            "audio/webm" => StartsWith(bytes, [0x1A, 0x45, 0xDF, 0xA3]),
            "audio/mp4" or "audio/m4a" => HasAscii(bytes, 4, "ftyp"),
            // MP3 可以帶 ID3 標籤，也可以直接從一個 frame 開始（同步字 11 個 1）
            "audio/mpeg" or "audio/mp3" => HasAscii(bytes, 0, "ID3")
                                           || (bytes.Length >= 2 && bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0),
            "audio/wav" or "audio/x-wav" => HasAscii(bytes, 0, "RIFF") && HasAscii(bytes, 8, "WAVE"),
            _ => false,
        };

    private static bool StartsWith(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> prefix)
        => bytes.Length >= prefix.Length && bytes[..prefix.Length].SequenceEqual(prefix);

    private static bool HasAscii(ReadOnlySpan<byte> bytes, int offset, string ascii)
    {
        if (bytes.Length < offset + ascii.Length)
        {
            return false;
        }
        Span<byte> expected = stackalloc byte[ascii.Length];
        Encoding.ASCII.GetBytes(ascii, expected);
        return bytes.Slice(offset, ascii.Length).SequenceEqual(expected);
    }
}
