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

            // 作者／回覆者的帳號狀態與現在的暱稱：這一批的人一次問完，再就地濾掉、換名。
            var active = ActiveNamesAmong(rows.Select(r => r.Post.UserId)
                .Concat(repliesByPost.Values.SelectMany(g => g).Select(reply => reply.UserId)));

            return rows
                .Where(x => active.ContainsKey(x.Post.UserId))
                .Select(x => WithCurrentNames(x.Post.ToDomain(
                    repliesByPost.GetValueOrDefault(x.Post.Id, Array.Empty<RantReplyRecord>())
                        .Where(reply => active.ContainsKey(reply.UserId)),
                    x.LikeCount), active))
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

    public RantPost? GetPost(string rantId)
    {
        if (db != null)
        {
            var post = db.RantPosts.AsNoTracking().FirstOrDefault(x => x.Id == rantId && !x.IsHidden);
            if (post == null) return null;
            var replies = db.RantReplies.AsNoTracking().Where(x => x.RantPostId == rantId).ToList();
            // 牆上濾掉了，直接帶網址進來也要濾掉，否則擋的只是「不好找」而已
            var active = ActiveNamesAmong(replies.Select(x => x.UserId).Append(post.UserId));
            if (!active.ContainsKey(post.UserId)) return null;
            var likeCount = db.RantReactions.AsNoTracking()
                .Count(x => x.RantPostId == rantId && x.ReactionType == "UNDERSTAND");
            return WithCurrentNames(
                post.ToDomain(replies.Where(x => active.ContainsKey(x.UserId)), likeCount), active);
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
            var post = db.RantPosts.FirstOrDefault(x => x.Id == rantId);
            if (post == null)
            {
                return null;
            }

            var reactionUserId = userId.Trim();
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

        return WithCurrentNames(storage.UpdateOne<RantPost>("rantPosts", rantId, post => post.LikeCount += 1));
    }

    public RantPost? Reply(string rantId, string userId, string content, string? imageUrl = null, string? audioUrl = null, string? parentReplyId = null, string? thumbUrl = null)
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
                Nickname = CurrentNickname(userId),
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
        if (!PostIsVisible(rantId)) return null;

        if (db != null)
        {
            var reply = db.RantReplies.FirstOrDefault(x => x.Id == replyId && x.RantPostId == rantId);
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

            return BuildPost(rantId);
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
            return BuildPost(rantId);
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
    /// </summary>
    private bool PostIsVisible(string rantId)
    {
        if (db != null)
        {
            var row = db.RantPosts.AsNoTracking()
                .Where(x => x.Id == rantId)
                .Select(x => new { x.UserId, x.IsHidden })
                .FirstOrDefault();
            return row is { IsHidden: false }
                   && AccountStateService.IsActive(db, storage, row.UserId, DateTimeOffset.UtcNow);
        }

        var fromFile = storage.ReadCollection<RantPost>("rantPosts").FirstOrDefault(x => x.Id == rantId);
        return fromFile is { IsHidden: false }
               && AccountStateService.IsActive(db, storage, fromFile.UserId, DateTimeOffset.UtcNow);
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

    private RantPost? BuildPost(string rantId)
    {
        if (db == null)
        {
            return null;
        }

        // !IsHidden 跟 GetPost 同一套。擋在這裡而不是只擋在呼叫端，是因為
        // 「指望每個呼叫端都記得檢查」已經漏過一次了（LikeReply 的可見性檢查）。
        var post = db.RantPosts.AsNoTracking().FirstOrDefault(x => x.Id == rantId && !x.IsHidden);
        if (post == null)
        {
            return null;
        }

        var replies = db.RantReplies.AsNoTracking()
            .Where(x => x.RantPostId == rantId)
            .ToList();
        // 按讚／回覆／檢舉之後回給呼叫者的這一份，和牆上看到的要是同一份。
        //
        // 作者本人也要查。這裡本來只問了回覆者，等於預設「呼叫端已經擋過了」——
        // 四個呼叫端裡有三個確實擋了，第四個（LikeReply）沒有，這個函式就照樣
        // 把貼文交出去。擋在這裡的好處是不必指望每個未來的呼叫端都記得：
        // 多問一個 id 不會多一趟往返，ActiveAmong 本來就是一次問一整批。
        var active = ActiveNamesAmong(replies.Select(x => x.UserId).Append(post.UserId));
        if (!active.ContainsKey(post.UserId))
        {
            return null;
        }

        var likeCount = db.RantReactions.AsNoTracking()
            .Count(x => x.RantPostId == rantId && x.ReactionType == "UNDERSTAND");
        return WithCurrentNames(
            post.ToDomain(replies.Where(x => active.ContainsKey(x.UserId)), likeCount), active);
    }
}
