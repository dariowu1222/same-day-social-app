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
    private readonly NotificationService? notifications;

    public RantService(JsonStorageService storage, TodayAnalyzerService analyzer, ModerationService moderation, IServiceProvider services)
    {
        this.storage = storage;
        this.analyzer = analyzer;
        this.moderation = moderation;
        db = services.GetService<AppDbContext>();
        notifications = services.GetService<NotificationService>();
    }

    // 單張上限（與前端 PostMediaInput 的 9 張一致）
    private const int MaxImages = 9;

    public (RantPost? Post, ModerationResult Moderation) Create(string userId, string nickname, string content, RantMode mode, List<string>? hashTags = null, List<string>? imageUrls = null, string? audioUrl = null, List<string>? thumbUrls = null)
    {
        var check = moderation.Check(content);
        if (check.IsBlocked)
        {
            return (null, check);
        }

        // 原圖與縮圖必須同索引對齊，因此先配對再一起過濾，避免任一邊被濾掉造成錯位。
        var pairs = (imageUrls ?? [])
            .Select((url, i) => (
                Url: url?.Trim() ?? "",
                Thumb: thumbUrls != null && i < thumbUrls.Count ? thumbUrls[i]?.Trim() ?? "" : ""))
            .Where(p => p.Url.Length > 0)
            .Take(MaxImages)
            .ToList();
        var images = pairs.Select(p => p.Url).ToList();
        var thumbs = pairs.Select(p => p.Thumb).ToList();

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
            ImageUrls = images,
            ThumbUrls = thumbs,
            ImageUrl = images.FirstOrDefault(), // 第一張回填，維持舊前端/單圖顯示相容
            AudioUrl = audioUrl
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

            // 作者／回覆者的帳號狀態：這一批的人一次問完，再就地濾掉。
            var active = ActiveAmong(rows.Select(r => r.Post.UserId)
                .Concat(repliesByPost.Values.SelectMany(g => g).Select(reply => reply.UserId)));

            return rows
                .Where(x => active.Contains(x.Post.UserId))
                .Select(x => x.Post.ToDomain(
                    repliesByPost.GetValueOrDefault(x.Post.Id, Array.Empty<RantReplyRecord>())
                        .Where(reply => active.Contains(reply.UserId)),
                    x.LikeCount))
                .ToList();
        }

        var fromFile = storage.ReadCollection<RantPost>("rantPosts")
            .Where(x => !x.IsHidden && (!cursor.HasValue || x.CreatedAt < cursor.Value))
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToList();
        var activeInFile = ActiveAmong(fromFile.Select(x => x.UserId)
            .Concat(fromFile.SelectMany(x => x.Replies).Select(reply => reply.UserId)));
        return fromFile
            .Where(x => activeInFile.Contains(x.UserId))
            .Select(x => StripInactiveReplies(x, activeInFile))
            .ToList();
    }

    public RantPost? GetPost(string rantId)
    {
        if (db != null)
        {
            var post = db.RantPosts.AsNoTracking().FirstOrDefault(x => x.Id == rantId && !x.IsHidden);
            if (post == null) return null;
            var replies = db.RantReplies.AsNoTracking().Where(x => x.RantPostId == rantId).ToList();
            // 牆上濾掉了，直接帶網址進來也要濾掉，否則擋的只是「不好找」而已
            var active = ActiveAmong(replies.Select(x => x.UserId).Append(post.UserId));
            if (!active.Contains(post.UserId)) return null;
            var likeCount = db.RantReactions.AsNoTracking()
                .Count(x => x.RantPostId == rantId && x.ReactionType == "UNDERSTAND");
            return post.ToDomain(replies.Where(x => active.Contains(x.UserId)), likeCount);
        }

        var fromFile = storage.ReadCollection<RantPost>("rantPosts").FirstOrDefault(x => x.Id == rantId && !x.IsHidden);
        if (fromFile == null) return null;
        var activeInFile = ActiveAmong(
            fromFile.Replies.Select(x => x.UserId).Append(fromFile.UserId));
        return activeInFile.Contains(fromFile.UserId)
            ? StripInactiveReplies(fromFile, activeInFile)
            : null;
    }

    public RantPost? Understand(string rantId, string? userId = null)
    {
        if (!PostIsVisible(rantId)) return null;

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

    public RantPost? Reply(string rantId, string userId, string nickname, string content, string? imageUrl = null, string? audioUrl = null, string? parentReplyId = null, string? thumbUrl = null)
    {
        if (!PostIsVisible(rantId)) return null;

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
                ImageUrl = imageUrl,
                ThumbUrl = thumbUrl,
                AudioUrl = audioUrl
            });
            db.SaveChanges();
            NotifyReply(post.UserId, userId, rantId, content);
            return BuildPost(rantId);
        }

        var updated = storage.UpdateOne<RantPost>("rantPosts", rantId, post =>
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
        if (updated != null) NotifyReply(updated.UserId, userId, rantId, content);
        return updated;
    }

    private void NotifyReply(string authorId, string replierId, string rantId, string content)
    {
        if (string.IsNullOrWhiteSpace(authorId) || authorId == replierId) return;
        var preview = content.Trim();
        if (preview.Length > 40) preview = $"{preview[..40]}…";
        notifications?.Add(authorId, "RANT_REPLY", "你的樹洞有新回應 🌱", preview, "rant", rantId);
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
        if (!PostIsVisible(rantId)) return null;

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

    /// <summary>
    /// 牢騷牆的帳號狀態過濾。
    ///
    /// 為什麼這面牆特別需要：它是全 App 唯一不用登入就讀得到的內容
    /// （RantsController 的 GET 沒有 [Authorize]），而貼文把作者暱稱複寫了一份
    /// 在 rant_posts.nickname 上。自助註銷只洗 users 那邊的暱稱、自我介紹與照片，
    /// 牆上這份副本原封不動——所以不在這裡擋，一個已經按下「刪除帳號」的人，
    /// 他的本名和整串心事還會繼續掛在公開頁面上。封鎖也一樣：帳號停了，
    /// 內容照留，等於封了個寂寞。
    ///
    /// 濾掉之後整頁可能不足 limit。這是刻意接受的：游標走的是 created_at，
    /// 少幾筆不會讓後面的分頁漏掉或重複，而為了補滿而多撈一輪，
    /// 代價是每一次翻頁都多付一趟遠端資料庫往返。
    /// </summary>
    private HashSet<string> ActiveAmong(IEnumerable<string> userIds)
        => AccountStateService.ActiveAmong(
            db, storage, userIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList(),
            DateTimeOffset.UtcNow);

    /// <summary>
    /// 這篇現在還看得到嗎。看不到就當作不存在：牆上既然濾掉了，按讚、回覆、檢舉
    /// 也不該還通得過——否則拿著舊畫面上的 id，照樣能往一篇已經隱形的貼文裡寫東西。
    /// </summary>
    private bool PostIsVisible(string rantId)
    {
        var authorId = db != null
            ? db.RantPosts.AsNoTracking().Where(x => x.Id == rantId).Select(x => x.UserId).FirstOrDefault()
            : storage.ReadCollection<RantPost>("rantPosts").FirstOrDefault(x => x.Id == rantId)?.UserId;
        return authorId != null
               && AccountStateService.IsActive(db, storage, authorId, DateTimeOffset.UtcNow);
    }

    /// <summary>JSON 模式的回覆是巢狀的，要連子回覆一起濾。</summary>
    private static RantPost StripInactiveReplies(RantPost post, HashSet<string> active)
    {
        post.Replies = Keep(post.Replies);
        post.ReplyCount = post.Replies.Count;
        return post;

        List<RantReply> Keep(List<RantReply> replies)
            => replies
                .Where(reply => active.Contains(reply.UserId))
                .Select(reply => { reply.Replies = Keep(reply.Replies); return reply; })
                .ToList();
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
        // 按讚／回覆／檢舉之後回給呼叫者的這一份，和牆上看到的要是同一份
        var active = ActiveAmong(replies.Select(x => x.UserId));
        var likeCount = db.RantReactions.AsNoTracking()
            .Count(x => x.RantPostId == rantId && x.ReactionType == "UNDERSTAND");
        return post.ToDomain(replies.Where(x => active.Contains(x.UserId)), likeCount);
    }
}
