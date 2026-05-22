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

            return tasks
                .Select(x => x.ToDomain(
                    participants.GetValueOrDefault(x.Id, Array.Empty<TaskParticipantRecord>())))
                .ToList();
        }

        var tasksFromFile = storage.ReadCollection<SocialTask>("tasks");
        if (tasksFromFile.Count > 0)
        {
            return tasksFromFile;
        }

        tasksFromFile = SeedTasks();
        storage.WriteCollection("tasks", tasksFromFile);
        return tasksFromFile;
    }

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

            var participantCount = db.TaskParticipants.Count(x => x.TaskId == taskId);
            var joined = db.TaskParticipants.Any(x => x.TaskId == taskId && x.UserId == userId);
            if (!joined && participantCount < task.ParticipantLimit)
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
            return task.ToDomain(participants);
        }

        GetTasks();
        return storage.UpdateOne<SocialTask>("tasks", taskId, task =>
        {
            if (!task.ParticipantUserIds.Contains(userId) && task.ParticipantUserIds.Count < task.ParticipantLimit)
            {
                task.ParticipantUserIds.Add(userId);
            }
        });
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
