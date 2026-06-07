using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Application.Services;

public sealed class RantService
{
    private readonly JsonStorageService storage;
    private readonly TodayAnalyzerService analyzer;
    private readonly ModerationService moderation;
    private readonly AppDbContext? db;

    public RantService(JsonStorageService storage, TodayAnalyzerService analyzer, ModerationService moderation, IServiceProvider services)
    {
        this.storage = storage;
        this.analyzer = analyzer;
        this.moderation = moderation;
        db = services.GetService<AppDbContext>();
    }

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
            Nickname = string.IsNullOrWhiteSpace(nickname) ? "同頻使用者" : nickname.Trim(),
            Content = content.Trim(),
            Mode = mode,
            EmotionTags = analysis.EmotionTags
        };

        if (db != null)
        {
            db.RantPosts.Add(post.ToRecord());
            db.SaveChanges();
            return (post, check);
        }

        storage.InsertOne("rantPosts", post);
        return (post, check);
    }

    public List<RantPost> GetPublicPosts(string? userId = null)
    {
        if (db != null)
        {
            var posts = db.RantPosts.AsNoTracking()
                .Where(x => !x.IsHidden)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
            var postIds = posts.Select(x => x.Id).ToHashSet();
            var replies = db.RantReplies.AsNoTracking()
                .Where(x => postIds.Contains(x.RantPostId))
                .ToList()
                .GroupBy(x => x.RantPostId)
                .ToDictionary(x => x.Key, x => x.AsEnumerable());
            var reactions = db.RantReactions.AsNoTracking()
                .Where(x => postIds.Contains(x.RantPostId) && x.ReactionType == "UNDERSTAND")
                .ToList();
            var likeCounts = reactions
                .GroupBy(x => x.RantPostId)
                .ToDictionary(x => x.Key, x => x.Count());
            var likedByMeSet = !string.IsNullOrWhiteSpace(userId)
                ? reactions.Where(x => x.UserId == userId).Select(x => x.RantPostId).ToHashSet()
                : [];

            return posts
                .Select(x => x.ToDomain(
                    replies.GetValueOrDefault(x.Id, Array.Empty<RantReplyRecord>()),
                    likeCounts.GetValueOrDefault(x.Id),
                    likedByMeSet.Contains(x.Id)))
                .ToList();
        }

        return storage.ReadCollection<RantPost>("rantPosts")
            .Where(x => !x.IsHidden)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public RantPost? Understand(string rantId, string? userId = null)
    {
        if (db != null)
        {
            var post = db.RantPosts.FirstOrDefault(x => x.Id == rantId);
            if (post == null)
            {
                return null;
            }

            var reactionUserId = string.IsNullOrWhiteSpace(userId) ? post.UserId : userId.Trim();
            var existingReaction = db.RantReactions.FirstOrDefault(x =>
                x.RantPostId == rantId &&
                x.UserId == reactionUserId &&
                x.ReactionType == "UNDERSTAND");

            if (existingReaction != null)
            {
                // 已按過，收回愛心
                db.RantReactions.Remove(existingReaction);
            }
            else
            {
                // 新增愛心
                db.RantReactions.Add(new RantReactionRecord
                {
                    Id = $"reaction_{Guid.NewGuid():N}",
                    RantPostId = rantId,
                    UserId = reactionUserId,
                    ReactionType = "UNDERSTAND"
                });
            }
            db.SaveChanges();
            return BuildPost(post.Id, reactionUserId);
        }

        return storage.UpdateOne<RantPost>("rantPosts", rantId, post => post.LikeCount += 1);
    }

    public RantPost? Reply(string rantId, string userId, string nickname, string content, string? parentReplyId = null)
    {
        if (db != null)
        {
            var post = db.RantPosts.FirstOrDefault(x => x.Id == rantId);
            if (post == null)
            {
                return null;
            }

            db.RantReplies.Add(new RantReplyRecord
            {
                Id = $"reply_{Guid.NewGuid():N}",
                RantPostId = rantId,
                UserId = userId,
                Nickname = string.IsNullOrWhiteSpace(nickname) ? "同頻使用者" : nickname.Trim(),
                Content = content.Trim(),
                ParentReplyId = string.IsNullOrWhiteSpace(parentReplyId) ? null : parentReplyId.Trim()
            });
            db.SaveChanges();
            return BuildPost(rantId);
        }

        return storage.UpdateOne<RantPost>("rantPosts", rantId, post =>
        {
            post.Replies.Add(new RantReply
            {
                Id = $"reply_{Guid.NewGuid():N}",
                UserId = userId,
                Nickname = string.IsNullOrWhiteSpace(nickname) ? "同頻使用者" : nickname.Trim(),
                Content = content.Trim()
            });
            post.ReplyCount = post.Replies.Count;
        });
    }

    public RantPost? Report(string rantId, string? userId = null)
    {
        if (db != null)
        {
            var post = db.RantPosts.FirstOrDefault(x => x.Id == rantId);
            if (post == null)
            {
                return null;
            }

            var reportUserId = string.IsNullOrWhiteSpace(userId) ? post.UserId : userId.Trim();
            var existingReport = db.RantReports.Any(x => x.RantPostId == rantId && x.UserId == reportUserId);
            if (!existingReport)
            {
                post.ReportCount += 1;
                post.IsHidden = post.ReportCount >= 3;
                db.RantReports.Add(new RantReportRecord
                {
                    Id = $"report_{Guid.NewGuid():N}",
                    RantPostId = rantId,
                    UserId = reportUserId
                });
                db.SaveChanges();
            }
            return BuildPost(rantId);
        }

        return storage.UpdateOne<RantPost>("rantPosts", rantId, post =>
        {
            post.ReportCount += 1;
            if (post.ReportCount >= 3)
            {
                post.IsHidden = true;
            }
        });
    }

    private RantPost? BuildPost(string rantId, string? userId = null)
    {
        if (db == null)
        {
            return null;
        }

        var post = db.RantPosts.AsNoTracking().FirstOrDefault(x => x.Id == rantId);
        if (post == null)
        {
            return null;
        }

        var replies = db.RantReplies.AsNoTracking()
            .Where(x => x.RantPostId == rantId)
            .ToList();
        var reactions = db.RantReactions.AsNoTracking()
            .Where(x => x.RantPostId == rantId && x.ReactionType == "UNDERSTAND")
            .ToList();
        var likeCount = reactions.Count;
        var likedByMe = !string.IsNullOrWhiteSpace(userId) && reactions.Any(x => x.UserId == userId);
        return post.ToDomain(replies, likeCount, likedByMe);
    }
}
