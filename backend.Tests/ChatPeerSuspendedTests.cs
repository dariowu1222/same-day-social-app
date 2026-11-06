using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 對方被停權之後，這一側的聊天室該怎麼樣。
///
/// 被處置的那個人自己在 Program.cs 的帳號狀態中介軟體就被 401 擋住了，
/// 但另一邊完全不受影響——他照常打字、照常看到訊息送出去，而對面永遠讀不到。
/// 那比「不能傳」更糟，因為他不知道。
///
/// 讀取那一側刻意不擋（見 ChatService 開頭那段）：把對話藏起來，使用者看到的
/// 不是「安全了」，而是聊天紀錄憑空消失。所以這組測試的分界是
/// 「看得到、傳不出去」。
/// </summary>
public sealed class ChatPeerSuspendedTests
{
    private const string Me = "user_me";
    private const string Peer = "user_peer";
    private const string RoomId = "chat_ours";

    [Theory]
    [InlineData(AccountStatus.SUSPENDED)]
    [InlineData(AccountStatus.BANNED)]
    [InlineData(AccountStatus.DELETED)]
    public void AnUnusableAccountCannotBeMessaged(AccountStatus status)
    {
        var (service, _) = Build(nameof(AnUnusableAccountCannotBeMessaged) + status, status);

        Assert.False(service.IsPeerActive(Me, RoomId));
    }

    [Fact]
    public void AnOrdinaryPeerIsStillReachable()
    {
        var (service, _) = Build(nameof(AnOrdinaryPeerIsStillReachable), AccountStatus.ACTIVE);

        Assert.True(service.IsPeerActive(Me, RoomId));
    }

    /// <summary>
    /// 停權期滿的人要自己回來，不必等後台按任何按鈕——users.status 還停在
    /// SUSPENDED 是正常的（寫回 ACTIVE 的排程不是期限一到就跑）。
    /// </summary>
    [Fact]
    public void SomebodyWhoseSuspensionHasRunOutIsReachableAgain()
    {
        const string name = nameof(SomebodyWhoseSuspensionHasRunOutIsReachableAgain);
        var (service, _) = Build(name, AccountStatus.SUSPENDED, TestWorld.SuspensionOver);

        Assert.True(service.IsPeerActive(Me, RoomId));
    }

    [Fact]
    public void SomebodyStillInsideTheirSuspensionIsNot()
    {
        const string name = nameof(SomebodyStillInsideTheirSuspensionIsNot);
        var (service, _) = Build(name, AccountStatus.SUSPENDED, TestWorld.StillSuspended);

        Assert.False(service.IsPeerActive(Me, RoomId));
    }

    /// <summary>
    /// 對方已經退出的一人房不在這條規則裡：那是另一種狀態，交給既有流程，
    /// 不要讓它也顯示「這個帳號目前無法使用」。
    /// </summary>
    [Fact]
    public void ARoomWithNobodyElseInItIsNotCoveredByThisRule()
    {
        const string name = nameof(ARoomWithNobodyElseInItIsNotCoveredByThisRule);
        var (service, db) = Build(name, AccountStatus.ACTIVE);
        db.ChatRoomUsers.Remove(db.ChatRoomUsers.Single(x => x.UserId == Peer));
        db.SaveChanges();

        Assert.True(service.IsPeerActive(Me, RoomId));
    }

    /// <summary>
    /// 讀取不受影響——這是刻意的。對話憑空消失比「不能再傳」更難理解，
    /// 而且使用者常常還需要回頭看對方說過什麼（例如要去檢舉）。
    /// </summary>
    [Fact]
    public void TheConversationItselfIsStillReadable()
    {
        const string name = nameof(TheConversationItselfIsStillReadable);
        var (service, db) = Build(name, AccountStatus.BANNED);
        db.ChatMessages.Add(new ChatMessageRecord
        {
            Id = "message_old", ChatRoomId = RoomId, SenderId = Peer, Content = "之前說過的話",
        });
        db.SaveChanges();

        Assert.Single(service.GetMessages(RoomId));
    }

    /// <summary>列表那一側也要知道，否則進房前看不出差別。</summary>
    [Fact]
    public void TheRoomListReportsThePeerAsUnavailable()
    {
        const string name = nameof(TheRoomListReportsThePeerAsUnavailable);
        var (service, _) = Build(name, AccountStatus.SUSPENDED, TestWorld.StillSuspended);

        Assert.False(service.GetRoomOverviews(Me).Single().PeerActive);
    }

    [Fact]
    public void TheRoomListReportsAnOrdinaryPeerAsFine()
    {
        var (service, _) = Build(nameof(TheRoomListReportsAnOrdinaryPeerAsFine), AccountStatus.ACTIVE);

        Assert.True(service.GetRoomOverviews(Me).Single().PeerActive);
    }

    // ── 測試資料 ──

    private static (ChatService Service, AppDbContext Db) Build(
        string databaseName, AccountStatus peerStatus, DateTimeOffset? suspendedUntil = null)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.AddRange(TestWorld.User(Me), TestWorld.User(Peer, peerStatus, suspendedUntil));
        db.ChatRooms.Add(new ChatRoomRecord { Id = RoomId });
        db.ChatRoomUsers.AddRange(
            new ChatRoomUserRecord { Id = "member_me", ChatRoomId = RoomId, UserId = Me },
            new ChatRoomUserRecord { Id = "member_peer", ChatRoomId = RoomId, UserId = Peer });
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        var provider = services.BuildServiceProvider();

        return (new ChatService(TestWorld.Storage(), new AppSettingsService(provider), provider), db);
    }
}
