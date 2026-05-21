using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Application.Services;

public sealed class RantService(JsonStorageService storage, TodayAnalyzerService analyzer, ModerationService moderation)
{
    public (RantPost? Post, ModerationResult Moderation) Create(string userId, string nickname, string content, RantMode mode)
    {
        var check = moderation.Check(content);
        if (check.IsBlocked)
        {
            return (null, check);
        }

        var analysis = analyzer.Analyze(content, ResponseMode.JUST_LISTEN);
        var post = new RantPost
        {
            Id = $"rant_{Guid.NewGuid():N}",
            UserId = userId,
            Nickname = string.IsNullOrWhiteSpace(nickname) ? "匿名的今天" : nickname.Trim(),
            Content = content.Trim(),
            Mode = mode,
            EmotionTags = analysis.EmotionTags
        };

        storage.InsertOne("rantPosts", post);
        return (post, check);
    }

    public List<RantPost> GetPublicPosts()
    {
        return storage.ReadCollection<RantPost>("rantPosts")
            .Where(x => !x.IsHidden)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public RantPost? Understand(string rantId)
    {
        return storage.UpdateOne<RantPost>("rantPosts", rantId, post => post.LikeCount += 1);
    }

    public RantPost? Reply(string rantId, string userId, string nickname, string content)
    {
        return storage.UpdateOne<RantPost>("rantPosts", rantId, post =>
        {
            post.Replies.Add(new RantReply
            {
                Id = $"reply_{Guid.NewGuid():N}",
                UserId = userId,
                Nickname = string.IsNullOrWhiteSpace(nickname) ? "同頻的人" : nickname.Trim(),
                Content = content.Trim()
            });
            post.ReplyCount = post.Replies.Count;
        });
    }

    public RantPost? Report(string rantId)
    {
        return storage.UpdateOne<RantPost>("rantPosts", rantId, post =>
        {
            post.ReportCount += 1;
            if (post.ReportCount >= 3)
            {
                post.IsHidden = true;
            }
        });
    }
}
