using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 聊天室這邊的分界：新的關係不再成立，既有的對話留著。
///
/// 前者是 CreateRoom——牌是先發後滑的，卡片到手之後對方才被封鎖／註銷，右滑
/// 仍然走得到開房間那一步。後者刻意不擋：把對方說過的話從歷史裡抽掉，剩下的
/// 是一段只有自己在講話的對話，而被害人常常正需要那段紀錄去檢舉或報案。
/// 這組測試把「刻意不擋」也釘住，免得日後有人順手補上。
/// </summary>
public sealed class ChatServiceAccountStatusTests
{
    private const string Me = "user_me";
    private const string Peer = "user_peer";
    private const string ExistingRoom = "chat_existing";

    [Fact]
    public void RoomOpensBetweenTwoActiveAccounts()
    {
        var service = Build(nameof(RoomOpensBetweenTwoActiveAccounts), TestWorld.User(Peer));

        var room = service.CreateRoom([Me, Peer], "MATCH", "match_1");

        Assert.Equal(2, room.UserIds.Count);
    }

    [Fact]
    public void NoRoomWithADeletedAccount()
    {
        var service = Build(nameof(NoRoomWithADeletedAccount), TestWorld.User(Peer, AccountStatus.DELETED));

        Assert.Throws<InvalidOperationException>(() => service.CreateRoom([Me, Peer], "MATCH", "match_1"));
    }

    [Fact]
    public void NoRoomWithABannedAccount()
    {
        var service = Build(nameof(NoRoomWithABannedAccount), TestWorld.User(Peer, AccountStatus.BANNED));

        Assert.Throws<InvalidOperationException>(() => service.CreateRoom([Me, Peer], "MATCH", "match_1"));
    }

    /// <summary>停權還沒到期：不開。這是下一個測試的對照組。</summary>
    [Fact]
    public void NoRoomWhileSuspensionIsRunning()
    {
        var service = Build(
            nameof(NoRoomWhileSuspensionIsRunning),
            TestWorld.User(Peer, AccountStatus.SUSPENDED, TestWorld.StillSuspended));

        Assert.Throws<InvalidOperationException>(() => service.CreateRoom([Me, Peer], "MATCH", "match_1"));
    }

    /// <summary>停權到期：照常開房，不必等後台有人去按。</summary>
    [Fact]
    public void RoomOpensAgainOnceTheSuspensionIsOver()
    {
        var service = Build(
            nameof(RoomOpensAgainOnceTheSuspensionIsOver),
            TestWorld.User(Peer, AccountStatus.SUSPENDED, TestWorld.SuspensionOver));

        var room = service.CreateRoom([Me, Peer], "MATCH", "match_1");

        Assert.Equal(2, room.UserIds.Count);
    }

    // ── 刻意不擋 ──

    [Fact]
    public void ExistingRoomSurvivesThePeerBeingDeleted()
    {
        var service = Build(nameof(ExistingRoomSurvivesThePeerBeingDeleted), TestWorld.User(Peer, AccountStatus.DELETED));

        Assert.Contains(service.GetRoomsByUser(Me), x => x.Id == ExistingRoom);
    }

    [Fact]
    public void HistoryFromADeletedPeerIsStillReadable()
    {
        var service = Build(nameof(HistoryFromADeletedPeerIsStillReadable), TestWorld.User(Peer, AccountStatus.DELETED));

        Assert.Contains(service.GetMessages(ExistingRoom), x => x.SenderId == Peer);
    }

    // ── 測試資料 ──

    /// <summary>「我」和對方已經有一間房、房裡各有一則訊息；對方的狀態由呼叫端指定。</summary>
    private static ChatService Build(string databaseName, UserRecord peer)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.AddRange(TestWorld.User(Me), peer);
        db.ChatRooms.Add(new ChatRoomRecord { Id = ExistingRoom, SourceType = "MATCH", SourceId = "match_0" });
        db.ChatRoomUsers.AddRange(
            new ChatRoomUserRecord { Id = "chat_user_me", ChatRoomId = ExistingRoom, UserId = Me },
            new ChatRoomUserRecord { Id = "chat_user_peer", ChatRoomId = ExistingRoom, UserId = Peer });
        db.ChatMessages.AddRange(
            new ChatMessageRecord { Id = "message_me", ChatRoomId = ExistingRoom, SenderId = Me, Content = "嗨" },
            new ChatMessageRecord { Id = "message_peer", ChatRoomId = ExistingRoom, SenderId = Peer, Content = "嗨嗨" });
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        var provider = services.BuildServiceProvider();

        // 這組測試不碰聊天對象上限，但 ChatService 現在要靠它取上限值。
        // 給真的 AppSettingsService（讀不到 app_settings 就退回預設 100），
        // 不另外造假：假的那個遲早會跟真的行為分岔。
        return new ChatService(
            TestWorld.Storage(), new AppSettingsService(provider), provider);
    }
}
