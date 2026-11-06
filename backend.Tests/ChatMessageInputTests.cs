using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Application.Validation;
using SameDaySocialApp.Controllers;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Hubs;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 聊天的引用由誰決定。
///
/// SendMessageRequest 以前有三欄引用資訊：quotedMessageId、quotedSenderName、
/// quotedContent，整組由客戶端送上來，而且彼此之間沒有對帳——id 指向 A、
/// 名字寫 B、內容寫一段 B 從來沒說過的話，照樣會被存下來。做出來的東西看起來
/// 就是對方親口說過的句子，可以拿去檢舉或用來騷擾，而且它真的在資料庫裡，
/// 事後調閱也是這樣。
///
/// 現在只收 id，其餘由伺服器查原訊息。這組測試釘的是那次查詢的三個邊界：
/// 房間要對、收回的不能復活、名字要是現在的。
/// </summary>
public sealed class ChatMessageInputTests
{
    private const string Me = "user_me";
    private const string Peer = "user_peer";
    private const string MyRoom = "room_mine";
    private const string OtherRoom = "room_elsewhere";

    [Fact]
    public void QuotingAMessageInThisRoomCopiesWhatThatMessageActuallySays()
    {
        var sent = Service(nameof(QuotingAMessageInThisRoomCopiesWhatThatMessageActuallySays))
            .SendMessage(MyRoom, Me, "我懂", "message_peer");

        Assert.Equal("message_peer", sent.QuotedMessageId);
        Assert.Equal("對方說過的話", sent.QuotedContent);
    }

    /// <summary>
    /// 引用的名字即時從 users 取，和樹洞同一條規則：存下來的那一份是發話當下的
    /// 稽核紀錄，顯示用的要是現在的名字。
    /// </summary>
    [Fact]
    public void TheQuotedNameComesFromTheUsersTableNotFromTheCaller()
    {
        const string name = nameof(TheQuotedNameComesFromTheUsersTableNotFromTheCaller);
        var db = Seed(name);
        db.Users.Single(x => x.Id == Peer).Nickname = "改過名的對方";
        db.SaveChanges();

        Assert.Equal("改過名的對方", Service(db).SendMessage(MyRoom, Me, "我懂", "message_peer").QuotedSenderName);
    }

    /// <summary>
    /// 不限定房間的話，任何人都能把自己別間房的訊息「搬」到這一間，
    /// 等於把第三人的私訊轉貼給無關的人看。
    /// </summary>
    [Fact]
    public void AMessageFromAnotherRoomCannotBeQuotedHere()
    {
        var sent = Service(nameof(AMessageFromAnotherRoomCannotBeQuotedHere))
            .SendMessage(MyRoom, Me, "你看這個", "message_elsewhere");

        Assert.Null(sent.QuotedMessageId);
        Assert.Null(sent.QuotedContent);
    }

    [Fact]
    public void QuotingSomethingThatDoesNotExistLeavesNoQuote()
    {
        var sent = Service(nameof(QuotingSomethingThatDoesNotExistLeavesNoQuote))
            .SendMessage(MyRoom, Me, "你說過", "message_i_made_up");

        Assert.Null(sent.QuotedMessageId);
        Assert.Null(sent.QuotedContent);
    }

    /// <summary>收回的訊息不能靠引用復活——原本那段話由客戶端自己填，收回之後照樣引用得出來。</summary>
    [Fact]
    public void ARecalledMessageDoesNotComeBackThroughAQuote()
    {
        const string name = nameof(ARecalledMessageDoesNotComeBackThroughAQuote);
        var db = Seed(name);
        db.ChatMessages.Single(x => x.Id == "message_peer").IsRecalled = true;
        db.SaveChanges();

        var sent = Service(db).SendMessage(MyRoom, Me, "你剛剛說什麼", "message_peer");

        Assert.Equal("（訊息已收回）", sent.QuotedContent);
        Assert.DoesNotContain("對方說過的話", sent.QuotedContent);
    }

    /// <summary>
    /// quoted_content 是 varchar(2000)，而線上還留著幾則整串 base64 的舊訊息
    /// （最長 120 KB）。不截斷的話引用一則就是一個 Npgsql 例外變成 500。
    /// </summary>
    [Fact]
    public void QuotingAnEnormousOldMessageIsTruncatedToTheColumn()
    {
        const string name = nameof(QuotingAnEnormousOldMessageIsTruncatedToTheColumn);
        var db = Seed(name);
        db.ChatMessages.Single(x => x.Id == "message_peer").Content = new string('阿', 5000);
        db.SaveChanges();

        Assert.Equal(2000, Service(db).SendMessage(MyRoom, Me, "太長了", "message_peer").QuotedContent!.Length);
    }

    [Fact]
    public void NotQuotingAnythingStaysEmpty()
    {
        var sent = Service(nameof(NotQuotingAnythingStaysEmpty)).SendMessage(MyRoom, Me, "嗨");

        Assert.Null(sent.QuotedMessageId);
        Assert.Null(sent.QuotedSenderName);
        Assert.Null(sent.QuotedContent);
    }

    // ── 端點：訊息內容本身 ──

    /// <summary>
    /// 圖片與語音訊息的「內容」就是一串網址，而前端看到像媒體的字串會直接
    /// &lt;img src&gt; / &lt;audio src&gt; 下去。網址指向攻擊者的伺服器，對方一打開對話，
    /// 瀏覽器就替他送出一次請求——IP、裝置、確切的上線時間全都有了。
    /// </summary>
    [Theory]
    [InlineData("https://evil.example/pixel.png")]
    [InlineData("https://evil.example/beacon.mp3")]
    [InlineData("data:image/jpeg;base64,/9j/4AAQSkZJRg==")]
    public async Task AMessageWhoseContentIsSomebodyElsesMediaIsRejected(string content)
    {
        var result = await Send(nameof(AMessageWhoseContentIsSomebodyElsesMediaIsRejected) + content.Length, content);

        AssertRejected(result, "INVALID_MEDIA_URL");
    }

    /// <summary>
    /// 純文字與使用者自己貼的一般連結不受影響。訊息裡貼連結是正常用法，
    /// 而前端只有在整串內容就是一個媒體網址時才會把它渲染成圖片。
    /// </summary>
    [Theory]
    [InlineData("今天好累")]
    [InlineData("看這個 https://evil.example/a.png")]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    public async Task OrdinaryTextAndLinksStillGoThrough(string content)
    {
        var result = await Send(nameof(OrdinaryTextAndLinksStillGoThrough) + content.Length, content);

        Assert.Equal(content, Payload(result).Content);
    }

    [Fact]
    public async Task OurOwnUploadedImageStillGoesThrough()
    {
        var url = TestWorld.OwnMediaPrefix + "chats/0123456789abcdef0123456789abcdef.jpg";
        var result = await Send(nameof(OurOwnUploadedImageStillGoesThrough), url);

        Assert.Equal(url, Payload(result).Content);
    }

    /// <summary>chat_messages.content 是沒有長度的 text，前端輸入框也沒有上限。</summary>
    [Fact]
    public async Task AMessageLongerThanTheLimitIsRejected()
    {
        var result = await Send(
            nameof(AMessageLongerThanTheLimitIsRejected), new string('阿', InputLimits.MaxMessageLength + 1));

        AssertRejected(result, "FIELD_TOO_LONG");
    }

    /// <summary>界線本身要過，不然上面那條用「永遠拒絕」也會綠。</summary>
    [Fact]
    public async Task TheLengthLimitItselfIsFine()
    {
        var result = await Send(nameof(TheLengthLimitItselfIsFine), new string('阿', InputLimits.MaxMessageLength));

        Assert.Equal(InputLimits.MaxMessageLength, Payload(result).Content.Length);
    }

    /// <summary>
    /// Content 宣告為不可為 null，但 JSON 送 null 照樣會進來，
    /// 而服務層第一件事就是 content.Trim()——以前這裡是一個 500。
    /// </summary>
    [Fact]
    public async Task ANullContentDoesNotBlowUp()
    {
        Assert.Equal("", Payload(await Send(nameof(ANullContentDoesNotBlowUp), null!)).Content);
    }

    // ── 測試資料 ──

    private static void AssertRejected(ActionResult<ApiResponse<ChatMessage>> result, string code)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(code, Assert.IsType<ApiResponse<ChatMessage>>(bad.Value).Code);
    }

    private static ChatMessage Payload(ActionResult<ApiResponse<ChatMessage>> result)
    {
        var response = Assert.IsType<ApiResponse<ChatMessage>>(result.Value);
        Assert.NotNull(response.Data);
        return response.Data!;
    }

    private static Task<ActionResult<ApiResponse<ChatMessage>>> Send(string databaseName, string content)
    {
        var db = Seed(databaseName);
        db.ChatRoomUsers.AddRange(
            new ChatRoomUserRecord { Id = "member_me", ChatRoomId = MyRoom, UserId = Me },
            new ChatRoomUserRecord { Id = "member_peer", ChatRoomId = MyRoom, UserId = Peer });
        db.ChatRooms.Add(new ChatRoomRecord { Id = MyRoom });
        db.SaveChanges();

        var controller = new ChatsController(Service(db), TestWorld.MediaUrls(), new SilentHub())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Me)], "test")),
                },
            },
        };

        return controller.SendMessage(MyRoom, new SendMessageRequest(content));
    }

    /// <summary>推播到 SignalR 的那一段在這組測試裡沒有要測，收下就算。</summary>
    private sealed class SilentHub : IHubContext<ChatHub>
    {
        public IHubClients Clients { get; } = new NoClients();
        public IGroupManager Groups { get; } = new NoGroups();

        private sealed class NoClients : IHubClients
        {
            public IClientProxy All => new NoProxy();
            public IClientProxy AllExcept(IReadOnlyList<string> e) => new NoProxy();
            public IClientProxy Client(string c) => new NoProxy();
            public IClientProxy Clients(IReadOnlyList<string> c) => new NoProxy();
            public IClientProxy Group(string g) => new NoProxy();
            public IClientProxy GroupExcept(string g, IReadOnlyList<string> e) => new NoProxy();
            public IClientProxy Groups(IReadOnlyList<string> g) => new NoProxy();
            public IClientProxy User(string u) => new NoProxy();
            public IClientProxy Users(IReadOnlyList<string> u) => new NoProxy();
        }

        private sealed class NoProxy : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default)
                => Task.CompletedTask;
        }

        private sealed class NoGroups : IGroupManager
        {
            public Task AddToGroupAsync(string c, string g, CancellationToken ct = default) => Task.CompletedTask;
            public Task RemoveFromGroupAsync(string c, string g, CancellationToken ct = default) => Task.CompletedTask;
        }
    }

    private static AppDbContext Seed(string databaseName)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.AddRange(TestWorld.User(Me), TestWorld.User(Peer));
        db.ChatMessages.AddRange(
            new ChatMessageRecord
            {
                Id = "message_peer", ChatRoomId = MyRoom, SenderId = Peer, Content = "對方說過的話",
            },
            new ChatMessageRecord
            {
                Id = "message_elsewhere", ChatRoomId = OtherRoom, SenderId = Peer, Content = "第三人的私訊",
            });
        db.SaveChanges();
        return db;
    }

    private static ChatService Service(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        var provider = services.BuildServiceProvider();

        return new ChatService(TestWorld.Storage(), new AppSettingsService(provider), provider);
    }

    private static ChatService Service(string databaseName) => Service(Seed(databaseName));
}
