using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 聊天室閒置到期的兩段式判斷。
///
/// 為什麼這組測試必須存在：這支是排程跑的，而寬限起點把第一次真正的寫入推到了
/// 功能上線後將近一個月。中間這段時間 Notify / Expire 一行都不會被執行——
/// 有錯的話，第一個發現的人會是某天早上九點收到通知的真實使用者，而通知寄出去
/// 收不回來、鎖定也沒有復原鍵。線上手動跑一次得到「0 通知 0 鎖定」只證明了讀取
/// 與守門是通的，證明不了寫入那半邊。
///
/// 以下每一條釘的都是「寫錯了線上會怎樣」，不是「程式碼現在長怎樣」。
/// </summary>
public sealed class ChatExpiryServiceTests
{
    // ── 第一段：通知 ──

    /// <summary>
    /// 到了門檻前一天就通知，而且只通知。鎖定是下一輪的事。
    /// 通知要進到每一位成員的收件匣，並且帶得回那間房——少了 linkId，
    /// 使用者收到「聊天室明天就會關閉」卻點不進去，等於沒給他那最後一天。
    /// </summary>
    [Fact]
    public void IdleRoomIsNotifiedButNotLocked()
    {
        var db = World(nameof(IdleRoomIsNotifiedButNotLocked));
        Room(db, "room_quiet", Ago(40));
        Members(db, "room_quiet", "user_a", "user_b");
        Message(db, "room_quiet", Ago(29));
        db.SaveChanges();

        var outcome = Service(db).Run();

        Assert.True(outcome.StorageReady);
        Assert.Equal(1, outcome.Scanned);
        Assert.Equal(1, outcome.Notified);
        Assert.Equal(0, outcome.Expired);

        var room = db.ChatRooms.Single();
        Assert.NotNull(room.ExpiryNotifiedAt);
        Assert.Null(room.ExpiredAt);

        var sent = db.Notifications.ToList();
        Assert.Equal(2, sent.Count);
        Assert.Equal(new[] { "user_a", "user_b" }, sent.Select(x => x.RecipientId).OrderBy(x => x));
        Assert.All(sent, n =>
        {
            Assert.Equal("CHAT_EXPIRING", n.Type);
            Assert.Equal("chat", n.LinkType);
            Assert.Equal("room_quiet", n.LinkId);
            // 門檻 30 天，通知時已經沉寂 29 天。數字是從設定算出來寫進文案的，
            // 寫死的話改了天數會送出與事實對不上的提醒。
            Assert.Contains("29", n.Body);
        });

        // 換一個 DbContext 重讀，確認東西真的落地了。見 Fresh() 的說明：
        // 同一個 context 上的斷言，SaveChanges 漏掉也照樣綠燈。
        var reloaded = Fresh(nameof(IdleRoomIsNotifiedButNotLocked));
        Assert.NotNull(reloaded.ChatRooms.Single().ExpiryNotifiedAt);
        Assert.Equal(2, reloaded.Notifications.Count());
    }

    /// <summary>還在講話的房間一個字都不該被動到。</summary>
    [Fact]
    public void ActiveRoomIsLeftAlone()
    {
        var db = World(nameof(ActiveRoomIsLeftAlone));
        Room(db, "room_live", Ago(40));
        Members(db, "room_live", "user_a", "user_b");
        Message(db, "room_live", Ago(2));
        db.SaveChanges();

        var outcome = Service(db).Run();

        Assert.Equal(0, outcome.Notified);
        Assert.Equal(0, outcome.Expired);
        Assert.Empty(db.Notifications);
        Assert.Null(db.ChatRooms.Single().ExpiryNotifiedAt);
    }

    /// <summary>
    /// 從沒有人說過話的房間，從開房那天起算——不是永遠不到期。
    /// 配對成功卻雙方都沒開口的房間佔著名額，是「上限 100 人」那條規則最常見的死水。
    /// </summary>
    [Fact]
    public void RoomWithNoMessagesCountsFromCreation()
    {
        var db = World(nameof(RoomWithNoMessagesCountsFromCreation));
        Room(db, "room_silent", Ago(29));
        Members(db, "room_silent", "user_a", "user_b");
        db.SaveChanges();

        var outcome = Service(db).Run();

        Assert.Equal(1, outcome.Notified);
        Assert.Equal(0, outcome.Expired);
    }

    // ── 第二段：鎖定，而且只在通知之後 ──

    /// <summary>
    /// 沉寂到天荒地老也不能直接鎖——沒通知過就不准鎖。
    ///
    /// 這條守的是排程漏跑（GitHub Actions 的 schedule 沒有 SLA）。少了它，
    /// 房間會從第 29 天跳到第 31 天被鎖，使用者在毫無預警的情況下發現不能再回話。
    /// 120 天的房間仍然只拿到通知，正是這個保護在生效。
    /// </summary>
    [Fact]
    public void AncientRoomStillOnlyGetsNotifiedFirst()
    {
        var db = World(nameof(AncientRoomStillOnlyGetsNotifiedFirst));
        Room(db, "room_ancient", Ago(120));
        Members(db, "room_ancient", "user_a", "user_b");
        Message(db, "room_ancient", Ago(100));
        db.SaveChanges();

        var outcome = Service(db).Run();

        Assert.Equal(1, outcome.Notified);
        Assert.Equal(0, outcome.Expired);
        Assert.Null(db.ChatRooms.Single().ExpiredAt);
    }

    /// <summary>
    /// 通知還沒滿 24 小時就不鎖。
    ///
    /// 「前一天通知」的承諾是以小時算的，不是以排程跑了幾次算的。同一天跑兩次
    /// （後台手動執行一次 + 當天的排程），第二次不該把早上剛通知的房間當場鎖掉。
    /// </summary>
    [Fact]
    public void FreshNoticeDoesNotLockYet()
    {
        var db = World(nameof(FreshNoticeDoesNotLockYet));
        Room(db, "room_warned", Ago(120), notifiedAt: DateTimeOffset.UtcNow.AddHours(-12));
        Members(db, "room_warned", "user_a", "user_b");
        Message(db, "room_warned", Ago(100));
        db.SaveChanges();

        var outcome = Service(db).Run();

        Assert.Equal(0, outcome.Notified);
        Assert.Equal(0, outcome.Expired);
        Assert.Null(db.ChatRooms.Single().ExpiredAt);
    }

    /// <summary>通知滿一天、仍然沒人回話，才鎖定——而且不會再補一則通知。</summary>
    [Fact]
    public void DayOldNoticeWithNoReplyLocksTheRoom()
    {
        var db = World(nameof(DayOldNoticeWithNoReplyLocksTheRoom));
        Room(db, "room_done", Ago(120), notifiedAt: DateTimeOffset.UtcNow.AddHours(-25));
        Members(db, "room_done", "user_a", "user_b");
        Message(db, "room_done", Ago(100));
        db.SaveChanges();

        var outcome = Service(db).Run();

        Assert.Equal(0, outcome.Notified);
        Assert.Equal(1, outcome.Expired);
        Assert.NotNull(db.ChatRooms.Single().ExpiredAt);
        Assert.Empty(db.Notifications);

        // 鎖定必須真的寫進資料庫。少了這一句，漏掉 SaveChanges 的版本會讓排程
        // 回報「鎖了 N 間」、log 印成功，而實際一間都沒鎖——而測試全綠。
        Assert.NotNull(Fresh(nameof(DayOldNoticeWithNoReplyLocksTheRoom)).ChatRooms.Single().ExpiredAt);
    }

    // ── 有人回話之後的重置 ──

    /// <summary>
    /// 有人回話就救回來了，即使 expiry_notified_at 還留著上一輪的時間戳。
    ///
    /// 這是整支最容易寫錯的一條。直覺作法是在「送出訊息」時把 expiry_notified_at
    /// 清掉，但那會在熱路徑上多一次寫入；這裡改成比較「通知時間有沒有比最後活動晚」。
    /// 比錯方向的話，回了話的房間照樣會在隔天被鎖——使用者剛接上話就被關門，
    /// 而他做的事情完全正確。
    /// </summary>
    [Fact]
    public void ReplyAfterNoticeSavesTheRoom()
    {
        var db = World(nameof(ReplyAfterNoticeSavesTheRoom));
        var notifiedAt = Ago(31);
        Room(db, "room_revived", Ago(120), notifiedAt: notifiedAt);
        Members(db, "room_revived", "user_a", "user_b");
        Message(db, "room_revived", Ago(100));
        Message(db, "room_revived", Ago(1));
        db.SaveChanges();

        var outcome = Service(db).Run();

        Assert.Equal(0, outcome.Notified);
        Assert.Equal(0, outcome.Expired);
        var room = db.ChatRooms.Single();
        Assert.Null(room.ExpiredAt);
        // 舊的時間戳留著是刻意的（不在送訊息那條路上清掉），它只是不再算數
        Assert.Equal(notifiedAt, room.ExpiryNotifiedAt);
    }

    /// <summary>
    /// 回話之後又沉寂，重新走一次完整的 30 天，而且是「重新通知」不是「直接鎖」。
    ///
    /// 這條跟上一條是一體兩面：舊的 expiry_notified_at 不能被當成「已經警告過」，
    /// 讓第二輪跳過通知直接鎖房。
    /// </summary>
    [Fact]
    public void SilenceAfterAReplyStartsAFreshNotice()
    {
        var db = World(nameof(SilenceAfterAReplyStartsAFreshNotice));
        var oldNotice = Ago(31);
        Room(db, "room_again", Ago(120), notifiedAt: oldNotice);
        Members(db, "room_again", "user_a", "user_b");
        Message(db, "room_again", Ago(30));
        db.SaveChanges();

        var outcome = Service(db).Run();

        Assert.Equal(1, outcome.Notified);
        Assert.Equal(0, outcome.Expired);
        var room = db.ChatRooms.Single();
        Assert.Null(room.ExpiredAt);
        Assert.True(room.ExpiryNotifiedAt > oldNotice,
            "重新通知要更新時間戳，否則下一輪會立刻滿 24 小時而當場鎖定");
        Assert.Equal(2, db.Notifications.Count());
    }

    // ── 寬限起點 ──

    /// <summary>
    /// 寬限起點擋得住既有資料。
    ///
    /// 這正是線上的處境：功能上線時 360 間房裡有 358 間早就超過 30 天沒訊息。
    /// 少了這條下限，上線當天排程一跑，使用者一早打開就看到聊天室整排被鎖，
    /// 而他從來沒收到過任何提醒。
    /// </summary>
    [Fact]
    public void GracePeriodHoldsBackPreExistingRooms()
    {
        var db = World(nameof(GracePeriodHoldsBackPreExistingRooms),
                       floor: DateTimeOffset.UtcNow.AddHours(-1));
        Room(db, "room_legacy", Ago(400));
        Members(db, "room_legacy", "user_a", "user_b");
        Message(db, "room_legacy", Ago(300));
        db.SaveChanges();

        var outcome = Service(db).Run();

        Assert.Equal(1, outcome.Scanned);
        Assert.Equal(0, outcome.Notified);
        Assert.Equal(0, outcome.Expired);
        Assert.Empty(db.Notifications);
    }

    // ── 不該被掃到的 ──

    /// <summary>
    /// 已經鎖定的房間不再進入掃描。
    ///
    /// 不只是省事：再掃一次它會符合「已通知且超過 24 小時」而被重複寫入，
    /// 而且 Scanned 這個數字是後台用來判斷影響範圍的，混進已鎖定的房間就不準了。
    /// </summary>
    [Fact]
    public void LockedRoomIsNoLongerScanned()
    {
        var db = World(nameof(LockedRoomIsNoLongerScanned));
        Room(db, "room_closed", Ago(120), notifiedAt: Ago(31), expiredAt: Ago(30));
        Members(db, "room_closed", "user_a", "user_b");
        db.SaveChanges();

        var outcome = Service(db).Run();

        Assert.Equal(0, outcome.Scanned);
        Assert.Equal(0, outcome.Notified);
        Assert.Equal(0, outcome.Expired);
    }

    /// <summary>
    /// 沒有資料庫（JSON 儲存模式）時回報 StorageReady=false，不是假裝做完了。
    ///
    /// 三個 0 配上 StorageReady=true 會被後台顯示成「沒有到期的房間」，那是一句
    /// 謊話——真相是這支根本沒跑。排程那側也靠這個分辨「沒事」與「沒跑」。
    /// </summary>
    [Fact]
    public void MissingDatabaseIsReportedNotSilentlyTreatedAsDone()
    {
        var empty = new ServiceCollection().BuildServiceProvider();
        var service = new ChatExpiryService(
            empty,
            new AppSettingsService(empty),
            new NotificationService(TestWorld.Storage(), empty),
            NullLogger<ChatExpiryService>.Instance);

        var outcome = service.Run();

        Assert.False(outcome.StorageReady);
        Assert.Equal(0, outcome.Scanned);
    }

    /// <summary>
    /// 設定值壞掉要退回 30 天，不能被當成 0。
    ///
    /// 這個值是人手動改的。有人把天數打成「三十」而程式把它讀成 0 的話，下一次
    /// 排程會把全站每一間聊天室都判定為到期——一次操作打爆全站。這裡拿一間只沉寂
    /// 10 天的房間當探針：退回 30 天就什麼都不該發生。
    /// </summary>
    [Fact]
    public void UnreadableIdleDaysFallsBackInsteadOfSweepingEverything()
    {
        var db = World(nameof(UnreadableIdleDaysFallsBackInsteadOfSweepingEverything),
                       idleDays: "三十");
        Room(db, "room_young", Ago(20));
        Members(db, "room_young", "user_a", "user_b");
        Message(db, "room_young", Ago(10));
        db.SaveChanges();

        var outcome = Service(db).Run();

        Assert.Equal(0, outcome.Notified);
        Assert.Equal(0, outcome.Expired);
        Assert.Empty(db.Notifications);
    }

    // ── 測試資料 ──

    private static DateTimeOffset Ago(double days) => DateTimeOffset.UtcNow.AddDays(-days);

    /// <summary>
    /// 另開一個 DbContext 連到同一個 InMemory 資料庫。
    ///
    /// 為什麼不能只用原本那個：拿同一個 context 斷言，EF 會把還在被追蹤的那個物件
    /// 還給你——欄位在記憶體裡就是改過的，SaveChanges 漏掉也看不出來。重讀一次才
    /// 分得出「算出來了」和「寫進去了」。
    ///
    /// 這不是假想的風險：把 Expire() 裡的 db.SaveChanges() 刪掉跑一次變異測試，
    /// 100 個測試全數通過，包含三條專門斷言鎖定的。
    /// </summary>
    private static AppDbContext Fresh(string databaseName) => TestWorld.Db(databaseName);

    private static AppDbContext World(
        string databaseName, string idleDays = "30", DateTimeOffset? floor = null)
    {
        var db = TestWorld.Db(databaseName);
        db.AppSettings.Add(new AppSettingRecord
        {
            Key = AppSettingsService.IdleDaysKey,
            Value = idleDays,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        if (floor is { } f)
        {
            db.AppSettings.Add(new AppSettingRecord
            {
                Key = AppSettingsService.ExpirySinceKey,
                Value = f.ToString("o"),
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        db.SaveChanges();
        return db;
    }

    private static void Room(
        AppDbContext db, string id, DateTimeOffset createdAt,
        DateTimeOffset? notifiedAt = null, DateTimeOffset? expiredAt = null)
        => db.ChatRooms.Add(new ChatRoomRecord
        {
            Id = id,
            SourceId = "match_" + id,
            CreatedAt = createdAt,
            ExpiryNotifiedAt = notifiedAt,
            ExpiredAt = expiredAt,
        });

    private static void Members(AppDbContext db, string roomId, params string[] userIds)
    {
        foreach (var userId in userIds)
        {
            db.ChatRoomUsers.Add(new ChatRoomUserRecord
            {
                Id = $"cru_{roomId}_{userId}",
                ChatRoomId = roomId,
                UserId = userId,
                JoinedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private static void Message(AppDbContext db, string roomId, DateTimeOffset at)
        => db.ChatMessages.Add(new ChatMessageRecord
        {
            Id = $"msg_{roomId}_{at.Ticks}",
            ChatRoomId = roomId,
            SenderId = "user_a",
            Content = "...",
            CreatedAt = at,
        });

    private static ChatExpiryService Service(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        var provider = services.BuildServiceProvider();
        return new ChatExpiryService(
            provider,
            new AppSettingsService(provider),
            new NotificationService(TestWorld.Storage(), provider),
            NullLogger<ChatExpiryService>.Instance);
    }
}
