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

    // 查不到名字時的保底。顯示與寫入共用同一個字串，兩邊不該各寫一份。
    private const string DefaultNickname = "同頻使用者";

    public (RantPost? Post, ModerationResult Moderation) Create(string userId, string content, RantMode mode, List<string>? hashTags = null, List<string>? imageUrls = null, string? audioUrl = null, List<string>? thumbUrls = null)
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
            Nickname = CurrentNickname(userId),
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

    /// <summary>
    /// 牆上的貼文。viewerId 是現在在看的人（未登入是 null）——同一篇貼文，
    /// 貼文作者、回應的人、路過的人看到的回應不一樣（見 Assemble）。
    ///
    /// repliesAsSummary = true 時不送回應的內容，只送「有幾則」和「前三個回應的人叫什麼」
    /// ——牆上收合的卡片就只顯示這兩樣，回應要等使用者點開那一篇才抓（GET /api/rants/{id}）。
    /// 以前是每一頁都把每一篇的全部回應連同內文、圖片網址一起送，貼文一熱門，
    /// 一頁的大小就跟著回應數一起長，而絕大多數回應根本沒有人點開看。
    /// 預設 false：已經上架的 App 是直接拿列表裡的回應來畫的，不能改它拿到的東西。
    /// </summary>
    public List<RantPost> GetPublicPosts(
        DateTimeOffset? cursor = null, int limit = 20, string? viewerId = null, bool repliesAsSummary = false)
    {
        limit = Math.Clamp(limit, 1, 50);
        if (db != null)
        {
            // 作者刪掉的不上牆。底下的回應還留著，但只有帶著連結進來才看得到（見 BuildPost）。
            var query = db.RantPosts.AsNoTracking().Where(x => !x.IsHidden && x.DeletedAt == null);
            if (cursor.HasValue) query = query.Where(x => x.CreatedAt < cursor.Value);
            var rows = query
                .OrderByDescending(x => x.CreatedAt)
                .Take(limit)
                .Select(x => new
                {
                    Post = x,
                    LikeCount = db.RantReactions.Count(reaction =>
                        reaction.RantPostId == x.Id &&
                        reaction.RantReplyId == null &&
                        reaction.ReactionType == "UNDERSTAND")
                })
                .ToList();

            // 批次載入這批貼文的所有回覆（單一查詢），讓列表/詳情頁都能顯示回覆內容。
            // session pooler(5432)支援同請求多段查詢，故安全。
            var postIds = rows.Select(r => r.Post.Id).ToList();
            var repliesByPost = WallRepliesQuery(db, postIds, repliesAsSummary)
                .ToList()
                .GroupBy(reply => reply.RantPostId)
                .ToDictionary(g => g.Key, g => g.AsEnumerable());

            // 作者／回覆者的帳號狀態與現在的暱稱：這一批的人一次問完，再就地濾掉、換名。
            var active = ActiveNamesAmong(rows.Select(r => r.Post.UserId)
                .Concat(repliesByPost.Values.SelectMany(g => g).Select(reply => reply.UserId)));

            return rows
                .Where(x => active.ContainsKey(x.Post.UserId))
                .Select(x => Assemble(
                    x.Post,
                    repliesByPost.GetValueOrDefault(x.Post.Id, Array.Empty<RantReplyRecord>()),
                    x.LikeCount, active, viewerId))
                .Select(post => repliesAsSummary ? SummarizeReplies(post) : post)
                .ToList();
        }

        var fromFile = storage.ReadCollection<RantPost>("rantPosts")
            .Where(x => !x.IsHidden && (!cursor.HasValue || x.CreatedAt < cursor.Value))
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToList();
        var activeInFile = ActiveNamesAmong(fromFile.Select(x => x.UserId)
            .Concat(fromFile.SelectMany(x => x.Replies).Select(reply => reply.UserId)));
        return fromFile
            .Where(x => activeInFile.ContainsKey(x.UserId))
            .Select(x => WithCurrentNames(StripInactiveReplies(x, activeInFile), activeInFile))
            .ToList();
    }

    /// <summary>
    /// 牆上這一批貼文的回應。
    ///
    /// 只要摘要時，內文、圖片、語音、發文當下的名字都不拿——剩下的欄位剛好是
    /// Assemble 判斷「這個人看得到哪幾則」要用的那幾個（誰寫的、接在誰底下、
    /// 有沒有被藏），所以可見性的規則還是同一份，不必為了算則數另寫一套。
    ///
    /// internal static：測試要拿它去確認 Npgsql 翻得出 SQL，而且摘要那一句真的沒有拿內文。
    /// </summary>
    internal static IQueryable<RantReplyRecord> WallRepliesQuery(
        AppDbContext context, List<string> postIds, bool summaryOnly)
    {
        var query = context.RantReplies.AsNoTracking()
            .Where(reply => postIds.Contains(reply.RantPostId) && reply.DeletedAt == null);
        if (!summaryOnly) return query;

        return query.Select(reply => new RantReplyRecord
        {
            Id = reply.Id,
            RantPostId = reply.RantPostId,
            ParentReplyId = reply.ParentReplyId,
            UserId = reply.UserId,
            HiddenAt = reply.HiddenAt,
            HiddenBy = reply.HiddenBy,
            CreatedAt = reply.CreatedAt,
        });
    }

    /// <summary>
    /// 把一篇組好的貼文收成摘要：留下則數（Assemble 已經照這個人看得到的算好了）
    /// 和前三個回應的人的名字，回應本身拿掉。名字的順序跟卡片上疊頭像的順序一樣
    /// ——照對話的先後，同一個人只算一次。
    /// </summary>
    private static RantPost SummarizeReplies(RantPost post)
    {
        var names = new List<string>();
        Collect(post.Replies);
        post.ReplyNames = names;
        post.Replies = [];
        return post;

        void Collect(List<RantReply> replies)
        {
            foreach (var reply in replies)
            {
                if (names.Count >= 3) return;
                if (!names.Contains(reply.Nickname)) names.Add(reply.Nickname);
                Collect(reply.Replies);
            }
        }
    }

    public RantPost? GetPost(string rantId, string? viewerId = null)
    {
        if (db != null)
        {
            // 牆上濾掉了，直接帶網址進來也要濾掉，否則擋的只是「不好找」而已。
            // 規則全部在 BuildPost：隱藏的、作者停權的回 null；作者刪掉的回一個殼。
            return BuildPost(rantId, viewerId);
        }

        var fromFile = storage.ReadCollection<RantPost>("rantPosts").FirstOrDefault(x => x.Id == rantId && !x.IsHidden);
        if (fromFile == null) return null;
        var activeInFile = ActiveNamesAmong(
            fromFile.Replies.Select(x => x.UserId).Append(fromFile.UserId));
        return activeInFile.ContainsKey(fromFile.UserId)
            ? WithCurrentNames(StripInactiveReplies(fromFile, activeInFile), activeInFile)
            : null;
    }

    /// <summary>
    /// 幫這篇按一個「我懂」。
    ///
    /// userId 是必填的，而且沒有退路。它原本是選填、空的時候退回 post.UserId
    /// ——而呼叫端（RantsController）從來沒有傳過，所以每一個人按的讚都記在
    /// 貼文作者自己頭上：第一次按就寫進一列，之後 Any() 恆為 true，整個 App 的
    /// 樹洞愛心數封頂在 1。改成必填之後，漏傳會編不過，不會再安靜地記錯人。
    /// </summary>
    public RantPost? Understand(string rantId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        if (!PostIsVisible(rantId)) return null;

        if (db != null)
        {
            // 這裡以前會再把貼文整列讀一次，只為了確認它存在——PostIsVisible 已經確認過了
            var reactionUserId = userId.Trim();
            // rant_reply_id is null 這個條件以前不在。回覆的愛心也記在這張表、也帶著
            // rant_post_id，所以「這個人在這篇底下按過任何一則回覆」就會被當成
            // 「已經按過這篇」——貼文的愛心再也按不上去。下面三處算貼文愛心數的地方
            // 是同一個漏洞的另一面：回覆的愛心全被加進貼文的數字裡。
            var existingReaction = db.RantReactions.Any(x =>
                x.RantPostId == rantId &&
                x.RantReplyId == null &&
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
            return BuildPost(rantId, reactionUserId);
        }

        return WithCurrentNames(storage.UpdateOne<RantPost>("rantPosts", rantId, post => post.LikeCount += 1));
    }

    /// <summary>
    /// 把自己按在這篇上的愛心收回去。
    ///
    /// 沒按過就什麼都不做、照樣回傳貼文——跟 Understand 一樣是冪等的，連按兩下、
    /// 或畫面是舊的，都不該變成錯誤。只動貼文層級的那一列（rant_reply_id is null），
    /// 不會把同一個人按在底下回覆上的愛心一起拿掉。
    /// </summary>
    public RantPost? Unlike(string rantId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        if (!PostIsVisible(rantId)) return null;

        if (db != null)
        {
            var reactionUserId = userId.Trim();
            var mine = db.RantReactions
                .Where(x =>
                    x.RantPostId == rantId &&
                    x.RantReplyId == null &&
                    x.UserId == reactionUserId &&
                    x.ReactionType == "UNDERSTAND")
                .ToList();
            if (mine.Count > 0)
            {
                db.RantReactions.RemoveRange(mine);
                db.SaveChanges();
            }
            return BuildPost(rantId, reactionUserId);
        }

        // JSON 模式不記「誰按的」，只有一個數字，所以只能往回扣一，而且不能扣成負的
        return WithCurrentNames(storage.UpdateOne<RantPost>("rantPosts", rantId,
            post => post.LikeCount = Math.Max(0, post.LikeCount - 1)));
    }

    public RantPost? Reply(string rantId, string userId, string content, string? imageUrl = null, string? audioUrl = null, string? parentReplyId = null, string? thumbUrl = null)
    {
        var access = AccessOf(rantId);
        if (access is not { IsLive: true }) return null;

        if (db != null)
        {
            // 畫面是舊的時候會發生：對方剛收回，這邊還看得到那一則、按了「回應」。
            // 收回的列還在資料庫裡（保留 30 天），接上去的話這一則會掛在一則
            // 沒有人看得到的回應底下。
            if (!string.IsNullOrEmpty(parentReplyId)
                && db.RantReplies.AsNoTracking().Any(x => x.Id == parentReplyId && x.DeletedAt != null))
            {
                return null;
            }

            db.RantReplies.Add(new RantReplyRecord
            {
                Id = $"reply_{Guid.NewGuid():N}",
                RantPostId = rantId,
                ParentReplyId = parentReplyId,
                UserId = userId,
                Nickname = CurrentNickname(userId),
                Content = content.Trim(),
                ImageUrl = imageUrl,
                ThumbUrl = thumbUrl,
                AudioUrl = audioUrl
            });
            db.SaveChanges();
            NotifyReply(access.AuthorId, userId, rantId, content);
            return BuildPost(rantId, userId);
        }

        var updated = storage.UpdateOne<RantPost>("rantPosts", rantId, post =>
        {
            post.Replies.Add(new RantReply
            {
                Id = $"reply_{Guid.NewGuid():N}",
                UserId = userId,
                Nickname = CurrentNickname(userId),
                Content = content.Trim()
            });
            post.ReplyCount = post.Replies.Count;
        });
        if (updated != null) NotifyReply(updated.UserId, userId, rantId, content);
        return WithCurrentNames(updated);
    }

    private void NotifyReply(string authorId, string replierId, string rantId, string content)
    {
        if (string.IsNullOrWhiteSpace(authorId) || authorId == replierId) return;
        var preview = content.Trim();
        if (preview.Length > 40) preview = $"{preview[..40]}…";
        notifications?.Add(authorId, "RANT_REPLY", "你的樹洞有新回應 🌱", preview, "rant", rantId);
    }

    /// <summary>
    /// 幫某一則回覆按「我懂」。
    ///
    /// 貼文的愛心一直都有去重（rant_reactions 一列一個人），回覆的沒有——這裡原本
    /// 只有 reply.LikeCount += 1，連呼叫者是誰都沒傳進來，同一個帳號可以一直按，
    /// 上限只有速率限制的每分鐘 60 次。現在跟貼文走同一張表，多帶一個 rant_reply_id。
    ///
    /// 數字仍然存在 rant_replies.like_count 這個計數欄位，不是每次 count 回來：
    /// 列表頁一次載入一整頁的回覆，改成即時 count 等於每翻一頁多一支 GROUP BY。
    /// 反應列在這裡的角色是「這個人按過了沒」，不是顯示來源。
    /// </summary>
    public RantPost? LikeReply(string rantId, string replyId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        // 這一行以前不在。PostIsVisible 的註解點名了「按讚、回覆、檢舉」三件事都要擋，
        // 但四個互動端點裡只有 Understand／Reply／Report 掛了它，LikeReply 漏了。
        // 漏的後果不是「多按到一個讚」：它回傳的是 BuildPost(rantId)，也就是整篇貼文。
        // 拿著舊畫面上的 rantId + replyId 戳一下，就能把一篇作者已被停權／註銷的貼文
        // 連同所有回覆整筆取回來——而後台註銷是刻意不洗個資的。
        if (!ThreadIsOpen(rantId)) return null;

        if (db != null)
        {
            var reply = db.RantReplies.FirstOrDefault(x =>
                x.Id == replyId && x.RantPostId == rantId && x.DeletedAt == null);
            if (reply == null) return null;

            var alreadyLiked = db.RantReactions.Any(x =>
                x.RantReplyId == replyId &&
                x.UserId == userId &&
                x.ReactionType == "UNDERSTAND");
            if (!alreadyLiked)
            {
                db.RantReactions.Add(new RantReactionRecord
                {
                    Id = $"reaction_{Guid.NewGuid():N}",
                    RantPostId = rantId,
                    RantReplyId = replyId,
                    UserId = userId.Trim(),
                    ReactionType = "UNDERSTAND"
                });
                reply.LikeCount += 1;
                db.SaveChanges();
            }

            return BuildPost(rantId, userId);
        }

        return WithCurrentNames(storage.UpdateOne<RantPost>("rantPosts", rantId, post =>
        {
            void IncrementReply(List<RantReply> list)
            {
                var target = list.FirstOrDefault(x => x.Id == replyId);
                if (target != null) { target.LikeCount += 1; return; }
                foreach (var r in list) IncrementReply(r.Replies);
            }
            IncrementReply(post.Replies);
        }));
    }

    /// <summary>
    /// 把自己按在某一則回覆上的愛心收回去。
    ///
    /// 回覆的愛心數存在 rant_replies.like_count 這個計數欄位（理由見 LikeReply），
    /// 所以刪掉反應列的同時要把數字扣回來，而且只有「真的刪到一列」才扣——
    /// 沒按過的人戳這支不能把別人按的愛心扣掉。兩件事放在同一次 SaveChanges。
    /// </summary>
    public RantPost? UnlikeReply(string rantId, string replyId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        if (!ThreadIsOpen(rantId)) return null;

        if (db != null)
        {
            var reply = db.RantReplies.FirstOrDefault(x =>
                x.Id == replyId && x.RantPostId == rantId && x.DeletedAt == null);
            if (reply == null) return null;

            var reactionUserId = userId.Trim();
            var mine = db.RantReactions
                .Where(x =>
                    x.RantReplyId == replyId &&
                    x.UserId == reactionUserId &&
                    x.ReactionType == "UNDERSTAND")
                .ToList();
            if (mine.Count > 0)
            {
                db.RantReactions.RemoveRange(mine);
                reply.LikeCount = Math.Max(0, reply.LikeCount - 1);
                db.SaveChanges();
            }

            return BuildPost(rantId, userId);
        }

        return WithCurrentNames(storage.UpdateOne<RantPost>("rantPosts", rantId, post =>
        {
            void DecrementReply(List<RantReply> list)
            {
                var target = list.FirstOrDefault(x => x.Id == replyId);
                if (target != null) { target.LikeCount = Math.Max(0, target.LikeCount - 1); return; }
                foreach (var r in list) DecrementReply(r.Replies);
            }
            DecrementReply(post.Replies);
        }));
    }

    /// <summary>
    /// 標上「這篇／這則我按過愛心了沒」。
    ///
    /// 以前沒有這個欄位，所以前端的愛心永遠是空心的：按了數字會動，但離開再回來
    /// 看不出自己按過沒有，再按一次也沒有任何反應。這裡一次把這一批貼文裡
    /// 呼叫者按過的列撈回來（一趟查詢），貼文層級與回覆層級用 rant_reply_id
    /// 是否為 null 分開。
    ///
    /// 只回「我自己按過沒有」，不回「誰按過」——那會把匿名牆上的人對回帳號。
    /// </summary>
    public void MarkLiked(IReadOnlyCollection<RantPost> posts, string userId)
    {
        if (db == null || posts.Count == 0 || string.IsNullOrWhiteSpace(userId)) return;

        var postIds = posts.Select(x => x.Id).ToList();
        var mine = db.RantReactions.AsNoTracking()
            .Where(x => x.UserId == userId && x.ReactionType == "UNDERSTAND" && postIds.Contains(x.RantPostId))
            .Select(x => new { x.RantPostId, x.RantReplyId })
            .ToList();
        if (mine.Count == 0) return;

        var likedPosts = mine.Where(x => x.RantReplyId == null).Select(x => x.RantPostId).ToHashSet();
        var likedReplies = mine.Where(x => x.RantReplyId != null).Select(x => x.RantReplyId!).ToHashSet();
        foreach (var post in posts)
        {
            post.IsLiked = likedPosts.Contains(post.Id);
            Mark(post.Replies);
        }

        void Mark(List<RantReply> replies)
        {
            foreach (var reply in replies)
            {
                reply.IsLiked = likedReplies.Contains(reply.Id);
                Mark(reply.Replies);
            }
        }
    }

    public const string ReplyNotFound = "REPLY_NOT_FOUND";
    public const string ReplyHasChildren = "REPLY_HAS_CHILDREN";

    /// <summary>
    /// 收回自己的一則回應。
    ///
    /// 只能收回「還沒有人接話」的回應。底下已經有別人的回覆時不給收：那幾則的
    /// 引用框畫的是這一則的內容，收回之後它們就接在一則看不到的話底下。
    ///
    /// 收回不是刪除。這一列留著、標上 deleted_at，對所有人隱形，30 天後才由
    /// PurgeDeleted 真的刪掉。以前是當場刪列——有人可以留一句難聽的話、等貼文作者
    /// 看到就收回，之後沒有東西可以檢舉，後台也查不到。時間限制擋不了這種人
    /// （他幾秒內就收回），留存才擋得了。
    ///
    /// 這則回應身上的愛心照舊當場刪掉：那不是內容，留著沒有用。
    ///
    /// 貼文被作者刪掉之後還是可以收回（那時候底下的回應都還在，見 ThreadIsOpen）。
    /// 收回的如果是最後一則，那個殼也就沒有東西可看了，Post 會是 null 而 Error 也是
    /// null——那是「收回成功，而且這一串已經沒有了」，不是失敗。
    ///
    /// 「不是你的」和「不存在」回同一個錯：分開回等於告訴呼叫者某個 replyId 存不存在。
    /// </summary>
    public (RantPost? Post, string? Error) DeleteReply(string rantId, string replyId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return (null, ReplyNotFound);
        if (!ThreadIsOpen(rantId)) return (null, ReplyNotFound);

        if (db != null)
        {
            var reply = db.RantReplies.FirstOrDefault(x =>
                x.Id == replyId && x.RantPostId == rantId && x.UserId == userId && x.DeletedAt == null);
            if (reply == null) return (null, ReplyNotFound);
            if (db.RantReplies.Any(x => x.ParentReplyId == replyId && x.DeletedAt == null))
            {
                return (null, ReplyHasChildren);
            }

            db.RantReactions.RemoveRange(db.RantReactions.Where(x => x.RantReplyId == replyId));
            reply.DeletedAt = DateTimeOffset.UtcNow;
            db.SaveChanges();
            return (BuildPost(rantId, userId), null);
        }

        var error = ReplyNotFound;
        var updated = storage.UpdateOne<RantPost>("rantPosts", rantId, post =>
        {
            error = RemoveOwnReply(post.Replies, replyId, userId) ?? "";
            post.ReplyCount = post.Replies.Count;
        });
        return error.Length == 0 ? (WithCurrentNames(updated), null) : (null, error);
    }

    /// <summary>JSON 模式的回覆是巢狀的，要往下找。成功回 null，否則回錯誤代碼。</summary>
    private static string? RemoveOwnReply(List<RantReply> replies, string replyId, string userId)
    {
        var target = replies.FirstOrDefault(x => x.Id == replyId);
        if (target != null)
        {
            if (target.UserId != userId) return ReplyNotFound;
            if (target.Replies.Count > 0) return ReplyHasChildren;
            replies.Remove(target);
            return null;
        }

        foreach (var reply in replies)
        {
            var result = RemoveOwnReply(reply.Replies, replyId, userId);
            if (result != ReplyNotFound) return result;
        }
        return ReplyNotFound;
    }

    /// <summary>
    /// 貼文作者隱藏（或取消隱藏）自己貼文底下的一則回應。
    ///
    /// 被留了難聽的話，作者當下就能自己處理，不必等後台。隱藏之後：
    ///  ・其他人看不到這一則，也看不到接在它底下的那幾則
    ///  ・貼文作者還看得到（收起來的樣子），可以取消，也可以再檢舉
    ///  ・回應的人自己還看得到，而且不會被通知
    ///
    /// 這不是刪除，也不是處置：要讓那個人受到處置得走檢舉，由後台判斷。
    ///
    /// 不能藏自己的回應——那是「收回」的事。不能取消後台藏的（hidden_by = ADMIN）：
    /// 那種回應貼文作者本來就看不到，拿著舊畫面上的 id 來戳也一樣回「找不到」。
    ///
    /// 貼文已經被作者刪掉就不能再藏：那時候作者跟這一串已經沒有關係了。
    /// </summary>
    public (RantPost? Post, string? Error) SetReplyHidden(string rantId, string replyId, string userId, bool hidden)
    {
        if (string.IsNullOrWhiteSpace(userId)) return (null, ReplyNotFound);
        // JSON 儲存模式沒有這個功能：那個模式發不出 token，這支端點根本進不來
        if (db == null) return (null, ReplyNotFound);
        var access = AccessOf(rantId);
        if (access is not { IsLive: true } || access.AuthorId != userId) return (null, ReplyNotFound);

        var reply = db.RantReplies.FirstOrDefault(x =>
            x.Id == replyId && x.RantPostId == rantId && x.DeletedAt == null && x.UserId != userId);
        if (reply == null) return (null, ReplyNotFound);
        if (reply.HiddenAt != null && reply.HiddenBy != RantReplyHiddenBy.Author) return (null, ReplyNotFound);

        if (hidden && reply.HiddenAt == null)
        {
            reply.HiddenAt = DateTimeOffset.UtcNow;
            reply.HiddenBy = RantReplyHiddenBy.Author;
            db.SaveChanges();
        }
        else if (!hidden && reply.HiddenAt != null)
        {
            reply.HiddenAt = null;
            reply.HiddenBy = null;
            db.SaveChanges();
        }

        return (BuildPost(rantId, userId), null);
    }

    /// <summary>
    /// 作者刪掉自己的貼文。
    ///
    /// 照 Threads 的做法：貼文不見，底下別人的回應不跟著消失。以前是把回應、愛心、
    /// 檢舉紀錄連同貼文四張表一起刪掉——替每一個回過話的人把話也收回去，而且
    /// 一篇被檢舉的貼文只要作者自己刪掉，後台就什麼都查不到了。
    ///
    /// 現在只標上 deleted_at。牆上不再出現；帶著連結進來的人看到的是一個殼
    /// （「這則貼文已被作者刪除」）加上還留著的回應。內容本身留 30 天給後台查，
    /// 之後由 PurgeDeleted 清掉。
    ///
    /// 已經刪過的再刪一次回 false（呼叫端是 404），跟「不是你的」「不存在」同一個答案。
    /// </summary>
    public bool Delete(string rantId, string userId)
    {
        if (db != null)
        {
            var post = db.RantPosts.FirstOrDefault(x =>
                x.Id == rantId && x.UserId == userId && x.DeletedAt == null);
            if (post == null) return false;
            post.DeletedAt = DateTimeOffset.UtcNow;
            db.SaveChanges();
            return true;
        }

        var found = storage.ReadCollection<RantPost>("rantPosts").Any(x => x.Id == rantId && x.UserId == userId);
        if (!found) return false;
        storage.DeleteOne<RantPost>("rantPosts", rantId);
        return true;
    }

    /// <summary>
    /// 檢舉這篇。三個不同的人檢舉過就自動隱藏。
    ///
    /// userId 跟 Understand 一樣改成必填。原本空的時候退回 post.UserId，而呼叫端
    /// 從來沒傳過，所以每一筆檢舉都記在「被檢舉的人」自己名下——第一筆寫進去之後
    /// Any() 恆為 true，ReportCount 永遠卡在 1，三次自動隱藏整段是死碼。
    /// 線上資料正是這個樣子：三列檢舉紀錄，三列的 user_id 都等於貼文作者。
    ///
    /// 成功檢舉到第三次時這裡會回 null（貼文當場隱藏，BuildPost 就撈不到了），
    /// 呼叫端看到的是 404。那是對的：那一刻這篇確實已經不該被看見。
    /// </summary>
    public RantPost? Report(string rantId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        if (!PostIsVisible(rantId)) return null;

        if (db != null)
        {
            var post = db.RantPosts.FirstOrDefault(x => x.Id == rantId);
            if (post == null)
            {
                return null;
            }

            var reportUserId = userId.Trim();
            var existingReport = db.RantReports.Any(x => x.RantPostId == rantId && x.UserId == reportUserId);
            if (!existingReport)
            {
                post.ReportCount += 1;
                // 只往「隱藏」的方向走，不會往回。原本是直接指派 post.ReportCount >= 3，
                // 於是一篇被後台處置隱藏的貼文（is_hidden=true、report_count=0）只要被
                // 任何人檢舉一次，就會被重新算成 1 >= 3 = false 而重見天日——使用者
                // 用一個檢舉就解除了管理端的處置。
                if (post.ReportCount >= 3) post.IsHidden = true;
                db.RantReports.Add(new RantReportRecord
                {
                    Id = $"report_{Guid.NewGuid():N}",
                    RantPostId = rantId,
                    UserId = reportUserId
                });
                db.SaveChanges();
            }
            return BuildPost(rantId, reportUserId);
        }

        return WithCurrentNames(storage.UpdateOne<RantPost>("rantPosts", rantId, post =>
        {
            post.ReportCount += 1;
            if (post.ReportCount >= 3)
            {
                post.IsHidden = true;
            }
        }));
    }

    /// <summary>
    /// 牢騷牆的帳號狀態過濾。
    ///
    /// 為什麼這面牆特別需要：它是全 App 唯一不用登入就讀得到的內容
    /// （RantsController 的 GET 沒有 [Authorize]）。帳號停了內容照留的話，
    /// 等於封了個寂寞；一個已經按下「刪除帳號」的人，整串心事也還會掛在公開頁面上。
    /// 名字現在是即時取的（見 WithCurrentNames），所以這裡擋的是內容本身，不是名字。
    ///
    /// 順便一次把名字也問回來：要過濾的人和要顯示名字的人是同一批，
    /// 分成兩支查詢等於白跑一趟。
    ///
    /// 濾掉之後整頁可能不足 limit。這是刻意接受的：游標走的是 created_at，
    /// 少幾筆不會讓後面的分頁漏掉或重複，而為了補滿而多撈一輪，
    /// 代價是每一次翻頁都多付一趟遠端資料庫往返。
    /// </summary>
    private Dictionary<string, string> ActiveNamesAmong(IEnumerable<string> userIds)
        => AccountStateService.ActiveNamesAmong(
            db, storage, userIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList(),
            DateTimeOffset.UtcNow);

    /// <summary>
    /// 把要顯示的名字換成 users 裡現在的暱稱。
    ///
    /// 為什麼不是直接讀 rant_posts.nickname：那一欄是發文當下抄下來的副本，
    /// 之後沒有任何程式回頭更新它。實際的資料是——同一個人的九篇貼文停在四個
    /// 不同的舊名字，整面牆十七篇有十七篇的名字和作者現在的暱稱對不上。
    ///
    /// 為什麼不是改名時回去 UPDATE 那兩張表：改名有四個入口（個人頁、商用後台、
    /// 自助註銷的兩條分支），漏掉任何一個就又不同步；而且 rant_posts 與 rant_replies
    /// 都沒有 user_id 索引，每次改名都是兩次全表掃描。讀的時候換，只有一個地方要對。
    ///
    /// 那一欄沒有刪掉，但已經不是顯示來源了，現在的用途是「發文當下這個人叫什麼」
    /// 的稽核紀錄——正因為如此，寫入時才改成由伺服器查（見 CurrentNickname），
    /// 不再採信客戶端送上來的字串。
    /// </summary>
    private static RantPost WithCurrentNames(RantPost post, IReadOnlyDictionary<string, string> names)
    {
        post.Nickname = Display(post.UserId, post.Nickname);
        Rename(post.Replies);
        return post;

        void Rename(List<RantReply> replies)
        {
            foreach (var reply in replies)
            {
                reply.Nickname = Display(reply.UserId, reply.Nickname);
                Rename(reply.Replies);
            }
        }

        // 退回發文當下那份、再退回預設值。兩層都需要：商用後台改資料那條路
        // 完全沒有驗證（AdminUserService 只判斷 != null），users.nickname
        // 是可以被寫成空字串的，空字串掛在牆上就是一個沒有名字的人在講話。
        string Display(string userId, string stored)
        {
            var current = names.GetValueOrDefault(userId);
            if (!string.IsNullOrWhiteSpace(current)) return current.Trim();
            return string.IsNullOrWhiteSpace(stored) ? DefaultNickname : stored;
        }
    }

    /// <summary>
    /// JSON 儲存模式的互動端點只拿得到一篇貼文，名字得自己查回來。
    ///
    /// 這條分支實際上走不到（沒有資料庫就發不出 token，需要登入的路徑一條都進不來），
    /// 補它不是為了那個情境，是為了不讓兩種儲存模式有兩套顯示規則——真要分岔，
    /// 分岔的那一邊通常就是漏的那一邊。
    ///
    /// 改的是 storage.UpdateOne 回傳的那個物件，不會寫回檔案：UpdateOne 每次重讀檔案，
    /// 而且在 return 之前就已經寫完了。
    /// </summary>
    private RantPost? WithCurrentNames(RantPost? post)
    {
        if (post == null) return null;

        var ids = new List<string> { post.UserId };
        Collect(post.Replies);
        return WithCurrentNames(post, ActiveNamesAmong(ids));

        void Collect(List<RantReply> replies)
        {
            foreach (var reply in replies)
            {
                ids.Add(reply.UserId);
                Collect(reply.Replies);
            }
        }
    }

    /// <summary>
    /// 寫進 rant_posts.nickname / rant_replies.nickname 的那一份，由伺服器自己查。
    ///
    /// 這裡以前收的是客戶端送上來的字串：userId 取自 JWT，暱稱取自 request body，
    /// 兩者從來沒有被拿去對帳過。也就是說任何已登入的人改一個欄位，就能用任意
    /// 顯示名在這面公開牆上發文。顯示改成即時取名之後冒名已經看不到了，但這一欄
    /// 要當稽核紀錄用，記的就不能是當事人自己報的名字。
    ///
    /// 不讀 JWT 的 nickname claim：那同樣是登入當下的快照，改名不會重簽 token，
    /// 用它只是把一個快照換成另一個快照。
    /// </summary>
    private string CurrentNickname(string userId)
    {
        var name = db != null
            ? db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Nickname).FirstOrDefault()
            : storage.ReadCollection<User>("users").FirstOrDefault(u => u.Id == userId)?.Nickname;
        return string.IsNullOrWhiteSpace(name) ? DefaultNickname : name.Trim();
    }

    /// <summary>
    /// 這篇現在還看得到嗎。看不到就當作不存在：牆上既然濾掉了，按讚、回覆、檢舉
    /// 也不該還通得過——否則拿著舊畫面上的 id，照樣能往一篇已經隱形的貼文裡寫東西。
    ///
    /// 「看得到」有兩個條件，不是一個。這裡原本只問了作者的帳號狀態，沒問 is_hidden，
    /// 而牆上（GetPublicPosts）與詳情頁（GetPost）兩邊都是兩個條件一起濾的。差這一個
    /// 條件的後果是：被檢舉自動隱藏、或被後台處置隱藏的貼文，GET 回 404，四個互動
    /// 端點卻照樣把整篇連同所有回覆交出去——等於替隱藏起來的內容開了一扇後門。
    ///
    /// 作者刪掉的貼文也算看不到：不能再按愛心、不能再回應、不能再檢舉。
    /// 底下既有的回應是另一回事，見 ThreadIsOpen。
    /// </summary>
    private bool PostIsVisible(string rantId) => AccessOf(rantId) is { IsLive: true };

    /// <summary>
    /// 這一串底下的回應現在還能不能動（按愛心、收回愛心、收回自己的回應）。
    ///
    /// 比 PostIsVisible 多放行一種：作者已經刪掉的貼文。那種貼文底下的回應還留著，
    /// 回過話的人要能把自己那一則收回，別人按過的愛心也要能收。
    ///
    /// 被處置隱藏的（is_hidden）不放行，不管作者有沒有刪——那是整串都不該被看見。
    /// 作者刪掉之後，作者的帳號狀態就不再影響這一串：剩下的都是別人的話。
    /// </summary>
    private bool ThreadIsOpen(string rantId) => AccessOf(rantId) is { IsOpen: true };

    /// <summary>
    /// 一篇貼文現在的狀態。IsLive＝還看得到（PostIsVisible 的定義），
    /// IsOpen＝底下的回應還能動（ThreadIsOpen 的定義），AuthorId＝作者。
    /// </summary>
    private sealed record PostAccess(string AuthorId, bool IsLive, bool IsOpen);

    /// <summary>
    /// 把「這篇還在不在、作者是誰、作者的帳號能不能用」一趟查完。查無此篇回 null。
    ///
    /// 以前是兩趟：先讀貼文，再拿作者的 id 去 users 問帳號狀態；有些端點接著還會
    /// 第三趟把貼文整列再讀一次，只為了拿作者 id 或確認它存在。每個互動端點
    /// （愛心、回應、收回、隱藏）開頭都這樣走——而線上後端到資料庫一趟要五十毫秒上下，
    /// 這些都是使用者按下去之後在等的時間。
    ///
    /// 帳號狀態的判斷還是 AccountStateService.IsActive 那一個（字串版：認不得的值、
    /// 查無此人都算不能用），跟牆上濾作者用的是同一條規則。
    /// </summary>
    private PostAccess? AccessOf(string rantId)
    {
        var now = DateTimeOffset.UtcNow;

        if (db != null)
        {
            var row = PostAccessQuery(db, rantId).FirstOrDefault();
            if (row == null) return null;

            var authorActive = AccountStateService.IsActive(row.AuthorStatus, row.AuthorSuspendedUntil, now);
            return new PostAccess(
                row.AuthorId,
                IsLive: !row.IsHidden && row.DeletedAt == null && authorActive,
                IsOpen: !row.IsHidden && (row.DeletedAt != null || authorActive));
        }

        var fromFile = storage.ReadCollection<RantPost>("rantPosts").FirstOrDefault(x => x.Id == rantId);
        if (fromFile == null) return null;
        var live = !fromFile.IsHidden && AccountStateService.IsActive(db, storage, fromFile.UserId, now);
        return new PostAccess(fromFile.UserId, live, live);
    }

    /// <summary>AccessOf 用的那一句。internal static 是為了讓測試確認 Npgsql 翻得出 SQL。</summary>
    internal static IQueryable<PostAccessRow> PostAccessQuery(AppDbContext context, string rantId)
        => from post in context.RantPosts.AsNoTracking()
           where post.Id == rantId
           join user in context.Users.AsNoTracking() on post.UserId equals user.Id into authors
           from author in authors.DefaultIfEmpty()
           select new PostAccessRow
           {
               AuthorId = post.UserId,
               IsHidden = post.IsHidden,
               DeletedAt = post.DeletedAt,
               AuthorStatus = author != null ? author.Status : null,
               AuthorSuspendedUntil = author != null ? author.SuspendedUntil : null,
           };

    internal sealed class PostAccessRow
    {
        public string AuthorId { get; init; } = "";
        public bool IsHidden { get; init; }
        public DateTimeOffset? DeletedAt { get; init; }
        public string? AuthorStatus { get; init; }
        public DateTimeOffset? AuthorSuspendedUntil { get; init; }
    }

    /// <summary>JSON 模式的回覆是巢狀的，要連子回覆一起濾。</summary>
    private static RantPost StripInactiveReplies(RantPost post, IReadOnlyDictionary<string, string> active)
    {
        post.Replies = Keep(post.Replies);
        post.ReplyCount = post.Replies.Count;
        return post;

        List<RantReply> Keep(List<RantReply> replies)
            => replies
                .Where(reply => active.ContainsKey(reply.UserId))
                .Select(reply => { reply.Replies = Keep(reply.Replies); return reply; })
                .ToList();
    }

    /// <summary>
    /// 單篇貼文，照「誰在看」組出來。牆上的單篇、詳情頁、每一個互動端點回傳的那一份
    /// 都走這裡，所以三邊看到的一定是同一份。
    ///
    /// 回 null 的情況：不存在、被處置隱藏、作者停權或註銷——以及作者刪掉了、
    /// 而且底下已經沒有這個人看得到的回應。
    /// </summary>
    private RantPost? BuildPost(string rantId, string? viewerId)
    {
        if (db == null)
        {
            return null;
        }

        // !IsHidden 擋在這裡而不是只擋在呼叫端，是因為「指望每個呼叫端都記得檢查」
        // 已經漏過一次了（LikeReply 的可見性檢查）。
        // 貼文和它的愛心數同一趟拿（牆上那一句本來就是這樣寫的）。以前愛心數另外查一趟。
        var found = db.RantPosts.AsNoTracking()
            .Where(x => x.Id == rantId && !x.IsHidden)
            .Select(x => new
            {
                Post = x,
                LikeCount = db.RantReactions.Count(reaction =>
                    reaction.RantPostId == x.Id &&
                    reaction.RantReplyId == null &&
                    reaction.ReactionType == "UNDERSTAND")
            })
            .FirstOrDefault();
        if (found == null)
        {
            return null;
        }

        var post = found.Post;
        var replies = db.RantReplies.AsNoTracking()
            .Where(x => x.RantPostId == rantId && x.DeletedAt == null)
            .ToList();
        var active = ActiveNamesAmong(replies.Select(x => x.UserId).Append(post.UserId));

        if (post.DeletedAt != null)
        {
            // 作者刪掉的：只剩一個殼和底下的回應。作者的帳號狀態在這裡不看——
            // 他的內容已經不在了，剩下的都是別人的話。
            var shell = Assemble(post, replies, 0, active, viewerId);
            return shell.Replies.Count == 0 ? null : shell;
        }

        // 作者本人也要查。這裡本來只問了回覆者，等於預設「呼叫端已經擋過了」——
        // 四個呼叫端裡有三個確實擋了，第四個（LikeReply）沒有，這個函式就照樣
        // 把貼文交出去。擋在這裡的好處是不必指望每個未來的呼叫端都記得。
        if (!active.ContainsKey(post.UserId))
        {
            return null;
        }

        return Assemble(post, replies, found.LikeCount, active, viewerId);
    }

    /// <summary>
    /// 把一篇貼文和它的回應組成要送出去的樣子。「這個人看得到哪幾則回應」只有這裡決定。
    ///
    /// 一則回應看不看得到：
    ///  1. 收回的（deleted_at）：誰都看不到
    ///  2. 回應的人被停權或註銷：誰都看不到（跟以前一樣）
    ///  3. 被後台隱藏，或接在一則被後台隱藏的回應底下：誰都看不到
    ///  4. 被貼文作者隱藏，或接在一則被作者隱藏的回應底下：
    ///       貼文作者看得到（標上 IsHidden，前端收起來顯示）
    ///       寫這一則的人自己看得到（不標，他看到的跟平常一樣）
    ///       其他人看不到
    ///
    /// 「接在底下」的也一起藏，是因為那幾則的引用框畫的就是被藏那一則的內容，
    /// 只藏上面那則等於原文還掛在別人的回應裡。
    ///
    /// 父親被濾掉的回應不會跟著消失，會自己升成第一層（見 RecordMapper.ToDomain）。
    /// </summary>
    private static RantPost Assemble(
        RantPostRecord post,
        IEnumerable<RantReplyRecord> replies,
        int likeCount,
        IReadOnlyDictionary<string, string> active,
        string? viewerId)
    {
        var all = replies.Where(x => x.DeletedAt == null).ToList();
        var byId = new Dictionary<string, RantReplyRecord>();
        foreach (var reply in all) byId[reply.Id] = reply;

        var viewerIsAuthor = !string.IsNullOrEmpty(viewerId) && viewerId == post.UserId;
        var shown = new List<RantReplyRecord>();
        var hiddenForAuthor = new HashSet<string>();

        foreach (var reply in all)
        {
            if (!active.ContainsKey(reply.UserId)) continue;

            var cover = HiddenCover(reply, byId);
            if (cover == null)
            {
                shown.Add(reply);
            }
            else if (cover != RantReplyHiddenBy.Author)
            {
                // 後台藏的：誰都不給
            }
            else if (viewerIsAuthor)
            {
                shown.Add(reply);
                hiddenForAuthor.Add(reply.Id);
            }
            else if (!string.IsNullOrEmpty(viewerId) && reply.UserId == viewerId)
            {
                shown.Add(reply);
            }
        }

        var result = WithCurrentNames(post.ToDomain(shown, likeCount), active);
        Mark(result.Replies);

        if (post.DeletedAt != null)
        {
            // 殼：只留 id 和時間，其餘一律清空——包括作者是誰。
            // UserId 清掉之後呼叫端算出來的 IsMine 一定是 false，作者自己帶著連結
            // 回來也不會再看到「刪除貼文」。
            result.IsDeleted = true;
            result.UserId = "";
            result.Nickname = "";
            result.Content = "";
            result.Mode = RantMode.JUST_SAYING;
            result.EmotionTags = [];
            result.HashTags = [];
            result.ImageUrls = [];
            result.ThumbUrls = [];
            result.ImageUrl = null;
            result.AudioUrl = null;
            result.LikeCount = 0;
        }

        return result;

        void Mark(List<RantReply> list)
        {
            foreach (var reply in list)
            {
                if (hiddenForAuthor.Contains(reply.Id))
                {
                    reply.IsHidden = true;
                    // 親手藏的才能取消；跟著上一則一起被藏的，要取消的是上面那一則
                    reply.CanUnhide = byId[reply.Id].HiddenAt != null;
                }
                Mark(reply.Replies);
            }
        }
    }

    /// <summary>
    /// 這一則有沒有被隱藏蓋住——自己被藏，或往上任何一層被藏。
    /// 回 null＝沒有；否則回是誰藏的。一路上有後台藏的就算後台的（比較嚴的那個贏）。
    /// </summary>
    private static string? HiddenCover(RantReplyRecord reply, Dictionary<string, RantReplyRecord> byId)
    {
        string? cover = null;
        var seen = new HashSet<string>();
        var current = reply;
        while (current != null && seen.Add(current.Id))   // 資料成環時不會無限往上
        {
            if (current.HiddenAt != null)
            {
                // hidden_by 沒填的當成後台藏的：寧可多藏，不要把該藏的放出去
                if (current.HiddenBy != RantReplyHiddenBy.Author) return RantReplyHiddenBy.Admin;
                cover = RantReplyHiddenBy.Author;
            }

            current = current.ParentReplyId != null ? byId.GetValueOrDefault(current.ParentReplyId) : null;
        }
        return cover;
    }

    /// <summary>
    /// 把刪除超過保留期的樹洞內容真的清掉。由排程打（MaintenanceController）。
    ///
    /// 保留期從 deleted_at 起算，預設 30 天——隱私權政策上寫的就是這個數字，
    /// 要改的話兩邊一起改。
    ///
    /// 回應：收回滿 30 天的整列刪掉。
    ///
    /// 貼文：刪除滿 30 天的分兩種。
    ///  ・底下已經沒有任何回應的列 → 整列刪掉
    ///  ・底下還有回應 → 把內容清空（文字、圖片、語音、標籤、發文當下的名字），
    ///    列留著當殼。回應是別人的話，不能因為貼文作者的保留期到了就一起消失；
    ///    而 rant_replies.rant_post_id 是外鍵（on delete cascade），刪列會把它們全帶走。
    ///    「還有回應」是連收回中、還沒滿 30 天的也算：那幾列也有自己的保留期要走完。
    ///
    /// 可以重複打：清空過的殼不會再被算進 Redacted，等底下的回應都沒了才會被刪掉。
    /// </summary>
    public RantPurgeOutcome PurgeDeleted(int retentionDays = 30)
    {
        if (db == null) return new RantPurgeOutcome(0, 0, 0, false);

        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, retentionDays));

        var staleReplies = db.RantReplies
            .Where(x => x.DeletedAt != null && x.DeletedAt < cutoff)
            .ToList();
        if (staleReplies.Count > 0)
        {
            var replyIds = staleReplies.Select(x => x.Id).ToList();
            db.RantReactions.RemoveRange(
                db.RantReactions.Where(x => x.RantReplyId != null && replyIds.Contains(x.RantReplyId)));

            // 收回的時候底下不會有活著的回應，但那條規則是程式擋的、不是資料庫擋的。
            // 真有漏網的，讓它變成第一層，不要留著一個指向不存在的列的 parent_reply_id。
            var orphans = db.RantReplies
                .Where(x => x.ParentReplyId != null && replyIds.Contains(x.ParentReplyId) && !replyIds.Contains(x.Id))
                .ToList();
            foreach (var orphan in orphans) orphan.ParentReplyId = null;

            db.RantReplies.RemoveRange(staleReplies);
            db.SaveChanges();
        }

        var stalePosts = db.RantPosts
            .Where(x => x.DeletedAt != null && x.DeletedAt < cutoff)
            .ToList();
        var removed = 0;
        var redacted = 0;
        if (stalePosts.Count > 0)
        {
            var postIds = stalePosts.Select(x => x.Id).ToList();
            var stillHasReplies = db.RantReplies.AsNoTracking()
                .Where(x => postIds.Contains(x.RantPostId))
                .Select(x => x.RantPostId)
                .Distinct()
                .ToList()
                .ToHashSet();

            foreach (var post in stalePosts)
            {
                if (!stillHasReplies.Contains(post.Id))
                {
                    db.RantReactions.RemoveRange(db.RantReactions.Where(x => x.RantPostId == post.Id));
                    db.RantReports.RemoveRange(db.RantReports.Where(x => x.RantPostId == post.Id));
                    db.RantPosts.Remove(post);
                    removed++;
                    continue;
                }

                var alreadyEmpty = post.Content.Length == 0
                                   && post.ImageUrls.Length == 0
                                   && post.ImageUrl == null
                                   && post.AudioUrl == null
                                   && post.HashTags.Length == 0;
                if (alreadyEmpty) continue;

                post.Content = "";
                post.Nickname = "";
                post.EmotionTags = [];
                post.HashTags = [];
                post.ImageUrls = [];
                post.ThumbUrls = [];
                post.ImageUrl = null;
                post.AudioUrl = null;
                // 貼文本身的愛心也沒有意義了；回應身上的不動
                db.RantReactions.RemoveRange(
                    db.RantReactions.Where(x => x.RantPostId == post.Id && x.RantReplyId == null));
                redacted++;
            }

            db.SaveChanges();
        }

        return new RantPurgeOutcome(staleReplies.Count, removed, redacted, true);
    }
}

/// <summary>
/// 清一次的結果。Replies＝刪掉幾則收回的回應、Posts＝刪掉幾篇貼文、
/// Redacted＝清空幾篇底下還有回應的貼文。Ran 為 false 代表沒有資料庫，什麼都沒做。
/// </summary>
public sealed record RantPurgeOutcome(int Replies, int Posts, int Redacted, bool Ran);
