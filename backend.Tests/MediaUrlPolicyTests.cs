using SameDaySocialApp.Infrastructure.Media;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 「這個網址可不可以被存成使用者內容」。
///
/// 為什麼這條規則存在：頭像、發文圖片、語音、聊天訊息原本接受任意 https 網址，
/// 而前端看到像媒體的字串就直接 &lt;img src&gt; / &lt;audio src&gt; 下去。送一則內容是
/// 自家伺服器 1×1 圖片的「訊息」，對方一打開對話，瀏覽器就替攻擊者送出一次請求。
///
/// 判斷「會不會被自動載入」的規則必須和前端的 ChatRoom.isImageMedia /
/// ChatPage.isImagePreview 蓋住同一個集合——窄了會放行追蹤像素，
/// 寬了會把正常的聊天連結當成媒體擋下來。
/// </summary>
public sealed class MediaUrlPolicyTests
{
    private static readonly MediaUrlPolicy Policy = TestWorld.MediaUrls();

    private const string Own = TestWorld.OwnMediaPrefix + "rants/0123456789abcdef0123456789abcdef.jpg";

    // ── 自家的 ──

    [Fact]
    public void OurOwnStorageUrlIsFine()
    {
        Assert.True(Policy.IsOwn(Own));
        Assert.True(Policy.IsAllowedAsMessage(Own));
    }

    [Theory]
    [InlineData("https://evil.example/pixel.png")]
    [InlineData("https://evil.example/track.jpg?u=victim")]
    [InlineData("https://evil.example/beacon.mp3")]
    // 前綴只差一點點：網域是攻擊者的，路徑長得像我們的
    [InlineData("https://evil.example/storage/v1/object/public/media/rants/x.jpg")]
    public void SomebodyElsesMediaUrlIsNot(string url)
    {
        Assert.False(Policy.IsOwn(url));
        Assert.False(Policy.IsAllowedAsMessage(url));
    }

    /// <summary>
    /// 正式環境不收 data:。收的話就回到老路：前端上傳失敗退回 data URL、
    /// 整串 base64 寫進資料庫——chat_messages 現在那三則最長 120 KB 的訊息就是這樣來的。
    /// </summary>
    [Fact]
    public void ADataUrlIsNotOurStorage()
    {
        Assert.False(Policy.IsOwn("data:image/jpeg;base64,/9j/4AAQSkZJRg=="));
        Assert.False(Policy.IsAllowedAsMessage("data:image/jpeg;base64,/9j/4AAQSkZJRg=="));
    }

    [Fact]
    public void AnAbsurdlyLongUrlIsNotOurStorage()
    {
        Assert.False(Policy.IsOwn(TestWorld.OwnMediaPrefix + new string('a', MediaUrlPolicy.MaxUrlLength) + ".jpg"));
    }

    // ── 會不會被自動載入 ──

    [Theory]
    [InlineData("https://evil.example/a.png")]
    [InlineData("https://evil.example/a.JPEG")]
    [InlineData("https://evil.example/a.gif?v=2")]
    [InlineData("https://evil.example/a.webm")]
    [InlineData("https://evil.example/a.ogg")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("data:audio/webm;base64,GkXfo0=")]
    public void ThingsTheClientAutoLoadsCountAsMedia(string content)
    {
        Assert.True(MediaUrlPolicy.RendersAsMedia(content));
    }

    /// <summary>
    /// 純文字不受影響。訊息裡貼連結是正常用法，而前端只有在整串內容
    /// 就是一個媒體網址時才會把它渲染成圖片——規則窄一點點都會變成誤擋。
    /// </summary>
    [Theory]
    [InlineData("今天好累")]
    [InlineData("看這個 https://evil.example/a.png")]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    [InlineData("https://evil.example/a.png.html")]
    [InlineData("")]
    [InlineData(null)]
    public void PlainTextAndOrdinaryLinksAreNotMedia(string? content)
    {
        Assert.False(MediaUrlPolicy.RendersAsMedia(content));
        Assert.True(Policy.IsAllowedAsMessage(content));
    }

    // ── 沒接儲存體的本機開發 ──

    /// <summary>
    /// 本機沒接 Supabase 時 MediaController 會原樣退回 data URL，所以那條路要放行，
    /// 否則本機完全傳不了圖。正式環境走的是另一條（IsConfigured == true）。
    /// </summary>
    [Fact]
    public void WithoutStorageConfiguredOnlyDataUrlsPass()
    {
        var dev = new MediaUrlPolicy(new UnconfiguredStorage());

        Assert.True(dev.IsOwn("data:image/jpeg;base64,/9j/4AAQSkZJRg=="));
        Assert.False(dev.IsOwn("https://evil.example/pixel.png"));
    }

    private sealed class UnconfiguredStorage : IMediaStorage
    {
        public bool IsConfigured => false;
        public string PublicUrlPrefix => "";

        public Task<string> UploadAsync(MediaFolder folder, byte[] bytes, string contentType, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
