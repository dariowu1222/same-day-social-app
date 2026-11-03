using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Application.Services;

public sealed class TaskService
{
    private readonly JsonStorageService storage;
    private readonly AppDbContext? db;

    public TaskService(JsonStorageService storage, IServiceProvider services)
    {
        this.storage = storage;
        db = services.GetService<AppDbContext>();
    }

    public List<SocialTask> GetTasks()
    {
        if (db != null)
        {
            SeedDatabaseTasksIfEmpty();
            var tasks = db.Tasks.AsNoTracking()
                .OrderBy(x => x.CreatedAt)
                .ToList();
            var taskIds = tasks.Select(x => x.Id).ToHashSet();
            var participants = db.TaskParticipants.AsNoTracking()
                .Where(x => taskIds.Contains(x.TaskId))
                .ToList()
                .GroupBy(x => x.TaskId)
                .ToDictionary(x => x.Key, x => x.AsEnumerable());

            var active = ActiveParticipants(participants.Values.SelectMany(x => x).Select(x => x.UserId));

            return tasks
                .Select(x => x.ToDomain(
                    participants.GetValueOrDefault(x.Id, Array.Empty<TaskParticipantRecord>())
                        .Where(p => active.Contains(p.UserId))))
                .ToList();
        }

        var tasksFromFile = storage.ReadCollection<SocialTask>("tasks");
        if (tasksFromFile.Count == 0)
        {
            tasksFromFile = SeedTasks();
            storage.WriteCollection("tasks", tasksFromFile);
        }

        var activeInFile = ActiveParticipants(tasksFromFile.SelectMany(x => x.ParticipantUserIds));
        foreach (var task in tasksFromFile)
        {
            task.ParticipantUserIds = task.ParticipantUserIds.Where(activeInFile.Contains).ToList();
        }
        return tasksFromFile;
    }

    /// <summary>
    /// 參與者清單裡現在還在的人。
    ///
    /// 畫面上這份清單只用來算「3/12 人參加」和判斷「我參加了沒」，不顯示任何個資，
    /// 所以漏擋不會外洩什麼——真正的代價是名額。註銷／封鎖之後沒人會回頭把那一列
    /// 刪掉，於是幽靈會一直佔著位子，人數滿了之後真的想參加的人就加不進來，
    /// 而且畫面上那個數字永遠對不上實際有幾個人。
    ///
    /// 停權的人算暫時離開，期滿自動回到清單——所以不能只看 users.status，
    /// 把它寫回 ACTIVE 的那段程式要等有人開後台才會跑。
    /// </summary>
    private HashSet<string> ActiveParticipants(IEnumerable<string> userIds)
        => AccountStateService.ActiveAmong(
            db, storage, userIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList(),
            DateTimeOffset.UtcNow);

    public SocialTask? Join(string taskId, string userId)
    {
        if (db != null)
        {
            SeedDatabaseTasksIfEmpty();
            var task = db.Tasks.FirstOrDefault(x => x.Id == taskId);
            if (task == null)
            {
                return null;
            }

            var existing = db.TaskParticipants.AsNoTracking()
                .Where(x => x.TaskId == taskId)
                .Select(x => x.UserId)
                .ToList();
            // 名額要跟畫面上那個數字算同一套，否則一堆幽靈把 12 個位子佔滿之後，
            // 使用者看到的是「3/12」卻怎麼按都加不進來。
            var activeExisting = ActiveParticipants(existing);
            var joined = existing.Contains(userId);
            if (!joined && activeExisting.Count < task.ParticipantLimit)
            {
                db.TaskParticipants.Add(new TaskParticipantRecord
                {
                    Id = $"task_user_{Guid.NewGuid():N}",
                    TaskId = taskId,
                    UserId = userId
                });
                db.SaveChanges();
            }

            var participants = db.TaskParticipants.AsNoTracking()
                .Where(x => x.TaskId == taskId)
                .ToList();
            var active = ActiveParticipants(participants.Select(x => x.UserId));
            return task.ToDomain(participants.Where(x => active.Contains(x.UserId)));
        }

        GetTasks();
        var updated = storage.UpdateOne<SocialTask>("tasks", taskId, task =>
        {
            var activeExisting = ActiveParticipants(task.ParticipantUserIds);
            if (!task.ParticipantUserIds.Contains(userId) && activeExisting.Count < task.ParticipantLimit)
            {
                task.ParticipantUserIds.Add(userId);
            }
        });
        if (updated == null) return null;
        var activeInFile = ActiveParticipants(updated.ParticipantUserIds);
        updated.ParticipantUserIds = updated.ParticipantUserIds.Where(activeInFile.Contains).ToList();
        return updated;
    }

    private void SeedDatabaseTasksIfEmpty()
    {
        if (db == null || db.Tasks.Any())
        {
            return;
        }

        db.Tasks.AddRange(SeedTasks().Select(x => x.ToRecord()));
        db.SaveChanges();
    }

    private static List<SocialTask> SeedTasks()
    {
        return
        [
            NewTask("散步 20 分鐘", "不用急著聊天，先一起完成一段舒服的散步。", TaskCategory.WALK, "20 分鐘"),
            NewTask("今晚早睡", "各自把手機放下，明天再互相說一聲早。", TaskCategory.EARLY_SLEEP, "1 晚"),
            NewTask("讀一小段書", "讀一段文字，分享今天最有感的一句。", TaskCategory.READ, "30 分鐘"),
            NewTask("低壓運動", "做一點簡單伸展或走路，讓心情慢慢回來。", TaskCategory.EXERCISE, "15 分鐘"),
            NewTask("看一集影片", "各自看同一集電影或動畫，再交換一個感想。", TaskCategory.MOVIE, "1 集"),
            NewTask("工程師共學 25 分鐘", "用一個番茄鐘完成小進度，不用互相比較。", TaskCategory.CODING, "25 分鐘")
        ];
    }

    private static SocialTask NewTask(string title, string description, TaskCategory category, string duration)
    {
        return new SocialTask
        {
            Id = $"task_{Guid.NewGuid():N}",
            Title = title,
            Description = description,
            Category = category,
            Duration = duration,
            Difficulty = "EASY",
            ParticipantLimit = 12
        };
    }
}
