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

    public (RantPost? Post, ModerationResult Moderation) Create(string userId, string nickname, string content, RantMode mode, List<string>? hashTags = null, string? imageDataUrl = null, string? audioDataUrl = null)
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
            EmotionTags = analysis.EmotionTags,
            HashTags = hashTags?.Select(t => t.Trim()).Where(t => t.Length > 0).Take(5).ToList() ?? [],
            ImageDataUrl = imageDataUrl,
            AudioDataUrl = audioDataUrl
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

    public List<RantPost> GetPublicPosts(DateTimeOffset? cursor = null, int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);
        if (db != null)
        {
            var query = db.RantPosts.AsNoTracking().Where(x => !x.IsHidden);
            if (cursor.HasValue) query = query.Where(x => x.CreatedAt < cursor.Value);
            var rows = query
                .OrderByDescending(x => x.CreatedAt)
                .Take(limit)
                .Select(x => new
                {
                    Post = x,
                    LikeCount = db.RantReactions.Count(reaction =>
                        reaction.RantPostId == x.Id &&
                        reaction.ReactionType == "UNDERSTAND")
                })
                .ToList();

            // 批次載入這批貼文的所有回覆（單一查詢），讓列表/詳情頁都能顯示回覆內容。
            // session pooler(5432)支援同請求多段查詢，故安全。
            var postIds = rows.Select(r => r.Post.Id).ToList();
            var repliesByPost = db.RantReplies.AsNoTracking()
                .Where(reply => postIds.Contains(reply.RantPostId))
                .ToList()
                .GroupBy(reply => reply.RantPostId)
                .ToDictionary(g => g.Key, g => g.AsEnumerable());

            return rows
                .Select(x => x.Post.ToDomain(
                    repliesByPost.GetValueOrDefault(x.Post.Id, Array.Empty<RantReplyRecord>()),
                    x.LikeCount))
                .ToList();
        }

        return storage.ReadCollection<RantPost>("rantPosts")
            .Where(x => !x.IsHidden && (!cursor.HasValue || x.CreatedAt < cursor.Value))
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToList();
    }

    public RantPost? GetPost(string rantId)
    {
        if (db != null)
        {
            var post = db.RantPosts.AsNoTracking().FirstOrDefault(x => x.Id == rantId && !x.IsHidden);
            if (post == null) return null;
            var replies = db.RantReplies.AsNoTracking().Where(x => x.RantPostId == rantId).ToList();
            var likeCount = db.RantReactions.AsNoTracking()
                .Count(x => x.RantPostId == rantId && x.ReactionType == "UNDERSTAND");
            return post.ToDomain(replies, likeCount);
        }
        return storage.ReadCollection<RantPost>("rantPosts").FirstOrDefault(x => x.Id == rantId && !x.IsHidden);
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
            var existingReaction = db.RantReactions.Any(x =>
                x.RantPostId == rantId &&
                x.UserId == reactionUserId &&
                x.ReactionType == "UNDERSTAND");
            if (!existingReaction)
            {
                db.RantReactions.Add(new RantReactionRecord
                {
                    Id = $"reaction_{Guid.NewGuid():N}",
                    RantPostId = rantId,
                    UserId = reactionUserId,
                    ReactionType = "UNDERSTAND"
                });
                db.SaveChanges();
            }
            return BuildPost(post.Id);
        }

        return storage.UpdateOne<RantPost>("rantPosts", rantId, post => post.LikeCount += 1);
    }

    public RantPost? Reply(string rantId, string userId, string nickname, string content, string? imageDataUrl = null, string? audioDataUrl = null, string? parentReplyId = null)
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
                ParentReplyId = parentReplyId,
                UserId = userId,
                Nickname = string.IsNullOrWhiteSpace(nickname) ? "同頻使用者" : nickname.Trim(),
                Content = content.Trim(),
                ImageDataUrl = imageDataUrl,
                AudioDataUrl = audioDataUrl
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

    public RantPost? LikeReply(string rantId, string replyId)
    {
        if (db != null)
        {
            var reply = db.RantReplies.FirstOrDefault(x => x.Id == replyId && x.RantPostId == rantId);
            if (reply == null) return null;
            reply.LikeCount += 1;
            db.SaveChanges();
            return BuildPost(rantId);
        }

        return storage.UpdateOne<RantPost>("rantPosts", rantId, post =>
        {
            void IncrementReply(List<RantReply> list)
            {
                var target = list.FirstOrDefault(x => x.Id == replyId);
                if (target != null) { target.LikeCount += 1; return; }
                foreach (var r in list) IncrementReply(r.Replies);
            }
            IncrementReply(post.Replies);
        });
    }

    public bool Delete(string rantId, string userId)
    {
        if (db != null)
        {
            var exists = db.RantPosts.Any(x => x.Id == rantId && x.UserId == userId);
            if (!exists) return false;
            db.RantReplies.Where(x => x.RantPostId == rantId).ExecuteDelete();
            db.RantReactions.Where(x => x.RantPostId == rantId).ExecuteDelete();
            db.RantReports.Where(x => x.RantPostId == rantId).ExecuteDelete();
            db.RantPosts.Where(x => x.Id == rantId).ExecuteDelete();
            return true;
        }

        var found = storage.ReadCollection<RantPost>("rantPosts").Any(x => x.Id == rantId && x.UserId == userId);
        if (!found) return false;
        storage.DeleteOne<RantPost>("rantPosts", rantId);
        return true;
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

    private RantPost? BuildPost(string rantId)
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
        var likeCount = db.RantReactions.AsNoTracking()
            .Count(x => x.RantPostId == rantId && x.ReactionType == "UNDERSTAND");
        return post.ToDomain(replies, likeCount);
    }
}
