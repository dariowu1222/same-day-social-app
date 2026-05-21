using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Application.Services;

public sealed class TaskService(JsonStorageService storage)
{
    public List<SocialTask> GetTasks()
    {
        var tasks = storage.ReadCollection<SocialTask>("tasks");
        if (tasks.Count > 0)
        {
            return tasks;
        }

        tasks = SeedTasks();
        storage.WriteCollection("tasks", tasks);
        return tasks;
    }

    public SocialTask? Join(string taskId, string userId)
    {
        GetTasks();
        return storage.UpdateOne<SocialTask>("tasks", taskId, task =>
        {
            if (!task.ParticipantUserIds.Contains(userId) && task.ParticipantUserIds.Count < task.ParticipantLimit)
            {
                task.ParticipantUserIds.Add(userId);
            }
        });
    }

    private static List<SocialTask> SeedTasks()
    {
        return
        [
            NewTask("散步二十分鐘", "今天不急著聊天，先各自去走一小段路。", TaskCategory.WALK, "20 分鐘"),
            NewTask("今晚早睡挑戰", "約好今天提早放下手機，明天再分享睡得如何。", TaskCategory.EARLY_SLEEP, "1 晚"),
            NewTask("讀十頁書", "各自讀十頁，交換一句有感覺的句子。", TaskCategory.READ, "30 分鐘"),
            NewTask("低壓運動", "做一個不勉強自己的小運動。", TaskCategory.EXERCISE, "15 分鐘"),
            NewTask("一起看一集動畫", "不用即時聊天，看完再說一句心得。", TaskCategory.MOVIE, "1 集"),
            NewTask("工程師共學 25 分鐘", "各自完成一個小段落，結束後互相打卡。", TaskCategory.CODING, "25 分鐘")
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
