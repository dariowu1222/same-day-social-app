using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 任務參與者清單的帳號狀態過濾。
///
/// 這份清單在畫面上只用來算「3/12 人參加」和判斷「我參加了沒」，不顯示個資，
/// 所以漏擋不會外洩什麼——代價是名額。註銷／封鎖之後沒人會回頭刪掉那一列，
/// 幽靈會一直佔著位子，人數滿了之後真的想參加的人就加不進來。
/// </summary>
public sealed class TaskServiceAccountStatusTests
{
    private const string TaskId = "task_under_test";
    private const string Ghost = "user_ghost";
    private const string Me = "user_me";

    [Fact]
    public void ActiveParticipantIsListed()
    {
        var service = Build(nameof(ActiveParticipantIsListed), TestWorld.User(Ghost));

        Assert.Contains(Ghost, Participants(service));
    }

    [Fact]
    public void DeletedParticipantIsNotListed()
    {
        var service = Build(nameof(DeletedParticipantIsNotListed), TestWorld.User(Ghost, AccountStatus.DELETED));

        Assert.DoesNotContain(Ghost, Participants(service));
    }

    [Fact]
    public void BannedParticipantIsNotListed()
    {
        var service = Build(nameof(BannedParticipantIsNotListed), TestWorld.User(Ghost, AccountStatus.BANNED));

        Assert.DoesNotContain(Ghost, Participants(service));
    }

    /// <summary>停權還沒到期：先從清單上拿掉。這是下一個測試的對照組。</summary>
    [Fact]
    public void RunningSuspensionIsNotListed()
    {
        var service = Build(
            nameof(RunningSuspensionIsNotListed),
            TestWorld.User(Ghost, AccountStatus.SUSPENDED, TestWorld.StillSuspended));

        Assert.DoesNotContain(Ghost, Participants(service));
    }

    /// <summary>停權到期：自動回到清單，不必等後台有人去按。</summary>
    [Fact]
    public void ExpiredSuspensionReturnsToTheList()
    {
        var service = Build(
            nameof(ExpiredSuspensionReturnsToTheList),
            TestWorld.User(Ghost, AccountStatus.SUSPENDED, TestWorld.SuspensionOver));

        Assert.Contains(Ghost, Participants(service));
    }

    /// <summary>
    /// 名額要跟畫面上那個數字算同一套：幽靈佔滿之後，使用者看到的是沒滿，
    /// 按下去卻加不進來。
    /// </summary>
    [Fact]
    public void GhostsDoNotEatTheLastSeat()
    {
        var service = Build(
            nameof(GhostsDoNotEatTheLastSeat),
            TestWorld.User(Ghost, AccountStatus.DELETED),
            participantLimit: 1);

        var joined = service.Join(TaskId, Me);

        Assert.NotNull(joined);
        Assert.Contains(Me, joined!.ParticipantUserIds);
    }

    [Fact]
    public void JoinedListAlsoHidesGhosts()
    {
        var service = Build(nameof(JoinedListAlsoHidesGhosts), TestWorld.User(Ghost, AccountStatus.DELETED));

        var joined = service.Join(TaskId, Me);

        Assert.NotNull(joined);
        Assert.DoesNotContain(Ghost, joined!.ParticipantUserIds);
    }

    // ── 測試資料 ──

    private static List<string> Participants(TaskService service)
        => service.GetTasks().Single(x => x.Id == TaskId).ParticipantUserIds;

    /// <summary>一個任務、一位既有參與者（狀態由呼叫端指定），外加還沒參加的「我」。</summary>
    private static TaskService Build(string databaseName, UserRecord participant, int participantLimit = 12)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.AddRange(participant, TestWorld.User(Me));
        db.Tasks.Add(new SocialTaskRecord
        {
            Id = TaskId,
            Title = "散步 20 分鐘",
            Description = "不用急著聊天，先一起完成一段舒服的散步。",
            Category = TaskCategory.WALK.ToString(),
            Duration = "20 分鐘",
            ParticipantLimit = participantLimit
        });
        db.TaskParticipants.Add(new TaskParticipantRecord
        {
            Id = $"task_user_{participant.Id}",
            TaskId = TaskId,
            UserId = participant.Id
        });
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);

        return new TaskService(TestWorld.Storage(), services.BuildServiceProvider());
    }
}
