using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Controllers;
using SameDaySocialApp.Infrastructure.Media;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// POST /api/media 的輸入把關。
///
/// 這支端點用 service_role 金鑰寫 Supabase Storage，原本還帶 x-upsert: true，
/// 而物件路徑的第一段直接來自 request body 的 folder（只做了 Trim('/')）。
/// 兩件事合起來就是：任何登入者都能指定要寫到哪裡、並覆寫那個位置既有的檔案。
///
/// 現在路徑三段全部由伺服器產生，folder 只是一個白名單查表的鍵。
/// 下面的測試釘的是「不在白名單上的東西進不來」與「進得來的真的是它說的那種檔案」。
/// </summary>
public sealed class MediaUploadSecurityTests
{
    // 1×1 的實際檔案，檔頭是真的——否則連 magic bytes 那關都過不了。
    private const string TinyJpeg =
        "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0a"
        + "HBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAALCAABAAEBAREA/8QAFAABAAAAAAAAAAAAAAAAAAAACf/EABQQAQAA"
        + "AAAAAAAAAAAAAAAAAAD/2gAIAQEAAD8AKp//2Q==";

    // ── folder：白名單以外一律拒絕 ──

    [Theory]
    // 用 # 把伺服器產生的 GUID 擠成 fragment，請求實際打到別人的檔案上
    [InlineData("avatars/victim.jpg#")]
    // 用 ? 做同一件事，GUID 變成 query string
    [InlineData("avatars/victim.jpg?")]
    // .NET 的 Uri 會正規化 ..，於是跳出 bucket——service_role 對所有 bucket 都有權限
    [InlineData("../../other-bucket")]
    [InlineData("avatars/../../..")]
    // 沒有這個位置，但原本會被 Trim('/') 之後原樣當成路徑用
    [InlineData("anything-i-want")]
    [InlineData("")]
    public async Task AFolderOutsideTheWhitelistIsRejected(string folder)
    {
        var storage = new RecordingStorage();
        var result = await Upload(storage, TinyJpeg, folder);

        AssertFailed(result, "MEDIA_FOLDER_INVALID");
        Assert.False(storage.WasCalled);
    }

    [Fact]
    public async Task NoFolderAtAllIsRejectedRatherThanDefaulted()
    {
        var storage = new RecordingStorage();
        var result = await Upload(storage, TinyJpeg, null);

        AssertFailed(result, "MEDIA_FOLDER_INVALID");
        Assert.False(storage.WasCalled);
    }

    [Theory]
    [InlineData("avatars", MediaFolder.Avatars)]
    [InlineData("avatars/thumbs", MediaFolder.AvatarThumbs)]
    [InlineData("chats", MediaFolder.Chats)]
    [InlineData("chats/thumbs", MediaFolder.ChatThumbs)]
    [InlineData("rants", MediaFolder.Rants)]
    [InlineData("rants/thumbs", MediaFolder.RantThumbs)]
    public async Task TheSixFoldersTheAppActuallyUsesStillWork(string folder, MediaFolder expected)
    {
        var storage = new RecordingStorage();
        var result = await Upload(storage, TinyJpeg, folder);

        Assert.Equal("https://storage.example/ok", Payload(result).Url);
        Assert.Equal(expected, storage.Folder);
    }

    // ── 檔案類型 ──

    [Theory]
    [InlineData("text/html")]
    [InlineData("image/svg+xml")]
    [InlineData("application/octet-stream")]
    public async Task ATypeOutsideTheWhitelistIsRejected(string contentType)
    {
        var storage = new RecordingStorage();
        var result = await Upload(storage, $"data:{contentType};base64,PGh0bWw+PC9odG1sPg==", "chats");

        AssertFailed(result, "MEDIA_TYPE_NOT_ALLOWED");
        Assert.False(storage.WasCalled);
    }

    /// <summary>
    /// 光有白名單只擋得住老實申報的人。把同一份 HTML 標成 image/png 送上來，
    /// 得到的一樣是一個掛在 supabase.co、可公開存取的網頁。
    /// </summary>
    [Fact]
    public async Task HtmlDressedUpAsAPngIsRejected()
    {
        var storage = new RecordingStorage();
        var result = await Upload(storage, "data:image/png;base64,PGh0bWw+PHNjcmlwdD48L3NjcmlwdD48L2h0bWw+", "chats");

        AssertFailed(result, "MEDIA_TYPE_MISMATCH");
        Assert.False(storage.WasCalled);
    }

    [Fact]
    public async Task TooBigIsRejectedBeforeItReachesStorage()
    {
        // 檔頭是真的 JPEG，後面補到超過 5 MB
        var bytes = new byte[MediaContentTypes.MaxImageBytes + 1];
        bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF;

        var storage = new RecordingStorage();
        var result = await Upload(storage, "data:image/jpeg;base64," + Convert.ToBase64String(bytes), "chats");

        AssertFailed(result, "MEDIA_TOO_LARGE");
        Assert.False(storage.WasCalled);
    }

    /// <summary>
    /// iOS 的 WKWebView 錄音出來是 data:audio/mp4;codecs=mp4a.40.2;base64,...
    /// 原本的正規表示式不容許型別後面有參數，於是整個 iPhone 的語音訊息都上傳失敗，
    /// 再被前端的 catch 吞掉、退回原本的 data URL 存進資料庫。
    /// </summary>
    [Fact]
    public async Task ADataUrlWithCodecParametersIsAccepted()
    {
        var mp4 = new byte[16];
        "ftyp"u8.CopyTo(mp4.AsSpan(4));

        var storage = new RecordingStorage();
        var result = await Upload(
            storage, "data:audio/mp4;codecs=mp4a.40.2;base64," + Convert.ToBase64String(mp4), "chats");

        Assert.Equal("https://storage.example/ok", Payload(result).Url);
        Assert.Equal("audio/mp4", storage.ContentType);
    }

    [Fact]
    public async Task SomethingThatIsNotADataUrlIsRejected()
    {
        var storage = new RecordingStorage();
        var result = await Upload(storage, "https://evil.example/payload.png", "chats");

        AssertFailed(result, "MEDIA_INVALID");
        Assert.False(storage.WasCalled);
    }

    // ── 路徑：伺服器自己產生 ──

    /// <summary>
    /// 真正的 adapter 怎麼組路徑。白名單前綴 + GUID + 白名單副檔名，
    /// 三段都沒有呼叫端的影子，所以 # 、? 、.. 全部無從出現。
    /// </summary>
    [Theory]
    [InlineData(MediaFolder.Avatars, "avatars")]
    [InlineData(MediaFolder.RantThumbs, "rants/thumbs")]
    public void TheStoragePathIsBuiltOnlyFromServerSideConstants(MediaFolder folder, string expectedPrefix)
    {
        Assert.Equal(expectedPrefix, MediaFolders.PrefixOf(folder));
    }

    /// <summary>
    /// 公開網址裡不放 user id。樹洞是匿名發文的地方，路徑帶上作者等於
    /// 每張圖都附一張名牌——M2（發文 API 不回傳 UserId）會因此白做。
    /// </summary>
    [Fact]
    public void TheWhitelistCoversExactlyTheFoldersTheAppUses()
    {
        foreach (var folder in Enum.GetValues<MediaFolder>())
        {
            var prefix = MediaFolders.PrefixOf(folder);
            Assert.Equal(folder, MediaFolders.Parse(prefix));
            Assert.DoesNotContain("user", prefix, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── 測試資料 ──

    private static void AssertFailed(ActionResult<ApiResponse<MediaUploadResponse>> result, string code)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<MediaUploadResponse>>(bad.Value);
        Assert.Equal(code, body.Code);
    }

    private static MediaUploadResponse Payload(ActionResult<ApiResponse<MediaUploadResponse>> result)
    {
        var response = Assert.IsType<ApiResponse<MediaUploadResponse>>(result.Value);
        Assert.NotNull(response.Data);
        return response.Data!;
    }

    private static Task<ActionResult<ApiResponse<MediaUploadResponse>>> Upload(
        IMediaStorage storage, string dataUrl, string? folder)
        => new MediaController(storage, new ProductionEnvironment())
            .Upload(new MediaUploadRequest(dataUrl, folder), CancellationToken.None);

    /// <summary>上傳到哪、上傳了什麼——以及最重要的，到底有沒有被呼叫。</summary>
    private sealed class RecordingStorage : IMediaStorage
    {
        public bool WasCalled { get; private set; }
        public MediaFolder? Folder { get; private set; }
        public string? ContentType { get; private set; }

        public bool IsConfigured => true;
        public string PublicUrlPrefix => "https://storage.example/";

        public Task<string> UploadAsync(MediaFolder folder, byte[] bytes, string contentType, CancellationToken ct = default)
        {
            WasCalled = true;
            Folder = folder;
            ContentType = contentType;
            return Task.FromResult("https://storage.example/ok");
        }
    }

    private sealed class ProductionEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "tests";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
