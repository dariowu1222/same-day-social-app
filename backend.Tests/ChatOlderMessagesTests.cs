using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 聊天室往上滑載入更早的訊息。
///
/// 以前進房只拿得到最近 50 則，而且沒有任何辦法再往前——聊超過 50 則的對話，
/// 前面那些在 App 裡就看不到了。現在可以帶 before 往前翻頁。
/// </summary>
public sealed class ChatOlderMessagesTests
{
    private const string RoomId = "room_long";
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WithoutACursorYouGetTheNewestPageOldestFirst()
    {
        var service = Build(nameof(WithoutACursorYouGetTheNewestPageOldestFirst), count: 120);

        var page = service.GetMessages(RoomId);

        Assert.Equal(50, page.Count);
        Assert.Equal("m070", page[0].Id);
        Assert.Equal("m119", page[^1].Id);
    }

    /// <summary>一頁一頁往前翻，要剛好把整段對話走完：不漏、不重複、最後一頁不滿。</summary>
    [Fact]
    public void PagingBackwardsWalksTheWholeConversationExactlyOnce()
    {
        var service = Build(nameof(PagingBackwardsWalksTheWholeConversationExactlyOnce), count: 120);

        var newest = service.GetMessages(RoomId);
        var middle = service.GetMessages(RoomId, before: newest[0].CreatedAt);
        var oldest = service.GetMessages(RoomId, before: middle[0].CreatedAt);
        var nothing = service.GetMessages(RoomId, before: oldest[0].CreatedAt);

        Assert.Equal(("m020", "m069", 50), (middle[0].Id, middle[^1].Id, middle.Count));
        Assert.Equal(("m000", "m019", 20), (oldest[0].Id, oldest[^1].Id, oldest.Count));
        Assert.Empty(nothing);

        var all = oldest.Concat(middle).Concat(newest).Select(x => x.Id).ToList();
        Assert.Equal(120, all.Distinct().Count());
        Assert.Equal(all.OrderBy(x => x), all);
    }

    /// <summary>游標是「之前」，不含那一則本身——不然每翻一頁最舊那則就重複出現一次。</summary>
    [Fact]
    public void TheCursorMessageItselfIsNotReturnedAgain()
    {
        var service = Build(nameof(TheCursorMessageItselfIsNotReturnedAgain), count: 10);
        var newest = service.GetMessages(RoomId, limit: 4);

        var older = service.GetMessages(RoomId, limit: 4, before: newest[0].CreatedAt);

        Assert.DoesNotContain(older, x => x.Id == newest[0].Id);
        Assert.Equal(["m002", "m003", "m004", "m005"], older.Select(x => x.Id));
    }

    /// <summary>別間房的訊息不會因為時間比較早就混進來。</summary>
    [Fact]
    public void OlderMessagesFromAnotherRoomDoNotLeakIn()
    {
        var db = TestWorld.Db(nameof(OlderMessagesFromAnotherRoomDoNotLeakIn));
        db.ChatMessages.Add(Message("mine", RoomId, 10));
        db.ChatMessages.Add(Message("theirs", "room_other", 1));
        db.SaveChanges();

        var older = Service(db).GetMessages(RoomId, before: Start.AddMinutes(20));

        Assert.Equal(["mine"], older.Select(x => x.Id));
    }

    private static ChatService Build(string databaseName, int count)
    {
        var db = TestWorld.Db(databaseName);
        for (var i = 0; i < count; i++)
        {
            db.ChatMessages.Add(Message($"m{i:D3}", RoomId, i));
        }
        db.SaveChanges();
        return Service(db);
    }

    private static ChatMessageRecord Message(string id, string roomId, int minute) => new()
    {
        Id = id,
        ChatRoomId = roomId,
        SenderId = "user_a",
        Content = id,
        CreatedAt = Start.AddMinutes(minute),
    };

    private static ChatService Service(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        var provider = services.BuildServiceProvider();
        return new ChatService(TestWorld.Storage(), new AppSettingsService(provider), provider);
    }
}
