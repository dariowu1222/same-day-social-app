using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using MatchType = SameDaySocialApp.Domain.Enums.MatchType;

namespace SameDaySocialApp.Application.Services;

public sealed class MatchService
{
    /// <summary>一次發幾張卡。左右滑都會消耗候選，5 張配上小規模用戶一天就見底。</summary>
    private const int DeckSize = 10;

    /// <summary>
    /// 左滑的冷卻天數。右滑是永久表態，左滑只是「今天沒興趣」——對方的「今天」
    /// 會變，配對理由也跟著變，所以冷卻期滿就讓他回到牌堆。
    /// 候選池小的時候，這是牌堆唯一的回血來源。
    /// </summary>
    private const int PassCooldownDays = 30;

    private readonly JsonStorageService storage;
    private readonly AppDbContext? db;
    private readonly NotificationService? notifications;
    private readonly IServiceProvider services;
    private readonly ILogger<MatchService>? logger;

    public MatchService(JsonStorageService storage, IServiceProvider services)
    {
        this.storage = storage;
        this.services = services;
        db = services.GetService<AppDbContext>();
        notifications = services.GetService<NotificationService>();
        logger = services.GetService<ILogger<MatchService>>();
    }

    public List<MatchResponse> GetTodayMatches(string userId)
    {
        var users = LoadUsers();

        // 登入者的交友意圖（想認識的對象、年齡範圍）→ 用來篩選候選
        var me = users.FirstOrDefault(x => x.Id == userId);
        if (me == null)
        {
            return [];
        }

        // 候選池從「使用者」出發，不是從「今日」出發。
        //
        // 先前是拿 today_entries 當池子，等於沒發過今日的人對所有人隱形。
        // 2026-11-03 實測：78 位使用者裡只有 8 位發過今日，其餘 90% 根本不在
        // 任何人的牌堆裡——而且 onboarding 只收個人資料、不會建立今日記錄，
        // 所以一個剛註冊完、資料填得完整的人，什麼都沒做錯就是隱形的，
        // app 裡也沒有任何地方會告訴他。今日有發就當加分素材，沒發也要能被看到。
        var latestEntries = LoadMatchableEntries()
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.MaxBy(x => x.CreatedAt)!);

        // 自己的今日同樣可有可無：有就拿來比對事件與情緒，沒有就只比個人資料。
        latestEntries.TryGetValue(userId, out var current);

        // 暫停配對：本人暫停則不配對；其他暫停者也不出現在候選
        var pausedUserIds = LoadPausedUserIds();
        if (pausedUserIds.Contains(userId))
        {
            return [];
        }

        // 已表態過的人（右滑永久、左滑在冷卻期內）與封鎖關係，都不該再出現。
        var excluded = LoadExcludedUserIds(userId);

        var pool = users
            .Where(x => x.Id != userId
                        && !pausedUserIds.Contains(x.Id)
                        && !excluded.Contains(x.Id))
            .ToList();

        // 年齡範圍是硬界線：湊不滿一副牌就發幾張算幾張，不放寬來補。
        // 放寬過的卡片在畫面上和其他人長得一模一樣，使用者只會看到自己
        // 明明設了 25–30 卻跑出 33 歲的人，結論是偏好設定沒有用。
        var deck = RankCandidates(pool, current, latestEntries, me)
            .Take(DeckSize)
            .ToList();

        // 落地只發生在這裡，而且只對真的要發出去的這幾張。
        // 先前是在算分階段對「每一位候選」寫一列，所以 matches 有列不代表
        // 使用者看過那個人——任何靠「列是否存在」判斷已滑過的作法都會失效。
        return ApplyDisplayScore(MaterializeDeck(userId, current, deck, users));
    }

    // 依登入者交友意圖篩選候選：想認識的對象（性別）+ 年齡範圍。
    // 兩者都是硬界線，不因為候選不足而放寬——使用者明說不想看的人硬塞進去，
    // 比沒卡可滑更糟。
    // 資料缺漏（候選沒填性別/生日、或本人沒設偏好）一律不排除，避免真實用戶被濾光。
    private static bool MatchesPreferences(User? me, User? candidate)
    {
        if (me == null || candidate == null) return true;

        if (!string.IsNullOrWhiteSpace(me.LookingFor) && me.LookingFor != "不限" && !string.IsNullOrWhiteSpace(candidate.Gender))
        {
            var wantGender = me.LookingFor == "男生" ? "男" : me.LookingFor == "女生" ? "女" : null;
            if (wantGender != null && candidate.Gender != wantGender) return false;
        }

        var age = AgeOf(candidate.Birthday);
        if (age != null)
        {
            if (me.AgeMin != null && age < me.AgeMin) return false;
            if (me.AgeMax != null && age > me.AgeMax) return false;
        }
        return true;
    }

    private static int? AgeOf(DateOnly? birthday)
    {
        if (birthday == null) return null;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - birthday.Value.Year;
        if (birthday.Value > today.AddYears(-age)) age--;
        return age;
    }

    private HashSet<string> LoadPausedUserIds()
    {
        if (db != null)
        {
            return db.UserSettings.AsNoTracking().Where(s => s.PauseMatching).Select(s => s.UserId).ToHashSet();
        }
        return storage.ReadCollection<UserSettingRecord>("userSettings")
            .Where(s => s.PauseMatching).Select(s => s.UserId).ToHashSet();
    }

    /// <summary>
    /// 右滑（表達同頻）。
    ///
    /// 配對紀錄是依方向各存一列：A 看到 B 是 (A,B)，B 看到 A 是 (B,A)。
    /// 所以「互相喜歡」＝ 自己這列 UserLiked 為 true，且反向那列的 UserLiked
    /// 也是 true。兩邊都成立時才算配對成功，這時才自動開聊天室、才發通知。
    ///
    /// 先前的作法是對方一右滑就立刻通知你，而通知沒有任何去重——100 個人
    /// 右滑就是 100 則一模一樣的「有人對你有共鳴」，而且收到的人也不知道是誰、
    /// 沒有任何可執行的動作。現在只在真的配對成功時通知，數量等於實際配對數。
    /// </summary>
    public LikeResult? Like(string matchId, string callerId)
    {
        if (db != null)
        {
            var record = db.Matches.FirstOrDefault(x => x.Id == matchId);
            // 所有權驗證：這筆配對必須屬於目前登入者
            if (record == null || record.UserId != callerId)
            {
                return null;
            }

            // 冪等：已經右滑過的就不要再跑一次 CompleteMatch。
            // NotificationService.Add 沒有去重，重跑會讓對方收到第二則一模一樣的
            // 「你們互相同頻了」——正是先前花力氣消掉的通知轟炸。
            // 行動網路下回應掉包、使用者重按，都會走到這裡。
            if (record.UserLiked)
            {
                var prior = db.Matches.AsNoTracking()
                    .FirstOrDefault(x => x.UserId == record.MatchedUserId && x.MatchedUserId == callerId);
                return prior is { UserLiked: true }
                    ? CompleteMatch(record.ToDomain(), callerId, record.MatchedUserId, notify: false)
                    : new LikeResult(record.ToDomain(), false, null);
            }

            record.UserLiked = true;

            // 反向那一列：對方看到我的那筆。對方還沒被配到我時不存在，
            // 等他之後滑到我、按了右滑，就會從他那一側觸發同樣的判斷。
            var reciprocal = db.Matches
                .FirstOrDefault(x => x.UserId == record.MatchedUserId && x.MatchedUserId == callerId);
            var mutual = reciprocal is { UserLiked: true };

            if (mutual)
            {
                // 兩邊都標記「對方也喜歡我」，各自的卡片才看得出已配對
                record.MatchedUserLiked = true;
                reciprocal!.MatchedUserLiked = true;
            }

            db.SaveChanges();

            return mutual
                ? CompleteMatch(record.ToDomain(), callerId, record.MatchedUserId, notify: true)
                : new LikeResult(record.ToDomain(), false, null);
        }

        var existing = storage.ReadCollection<MatchRecord>("matches").FirstOrDefault(x => x.Id == matchId);
        if (existing == null || existing.UserId != callerId)
        {
            return null;
        }

        var all = storage.ReadCollection<MatchRecord>("matches");
        var other = all.FirstOrDefault(x => x.UserId == existing.MatchedUserId && x.MatchedUserId == callerId);
        var mutualFile = other is { UserLiked: true };

        if (existing.UserLiked)
        {
            return mutualFile
                ? CompleteMatch(existing, callerId, existing.MatchedUserId, notify: false)
                : new LikeResult(existing, false, null);
        }

        var liked = storage.UpdateOne<MatchRecord>("matches", matchId, match =>
        {
            match.UserLiked = true;
            if (mutualFile) match.MatchedUserLiked = true;
        })!;
        if (mutualFile)
        {
            storage.UpdateOne<MatchRecord>("matches", other!.Id, match => match.MatchedUserLiked = true);
            return CompleteMatch(liked, callerId, existing.MatchedUserId, notify: true);
        }

        return new LikeResult(liked, false, null);
    }

    /// <summary>
    /// 左滑（略過）。
    ///
    /// 記下時間而不是只記一個旗標，是因為左滑只排除冷卻期（PassCooldownDays），
    /// 期滿就讓對方回到牌堆——「今天沒興趣」不等於「永遠沒興趣」，
    /// 而且在候選池還小的時候，這是牌堆唯一的回血來源。
    ///
    /// 重複呼叫是安全的（結果一樣），所以前端可以放心重試。
    ///
    /// 時間一律覆寫，不能因為「已經有值就跳過」。冷卻期滿後對方會回到牌堆，
    /// 這時再左滑一次必須重新計時；若沿用舊時間，他就會每次都立刻再回來，
    /// 使用者滑幾次都趕不走，而 API 每次都回成功。
    /// </summary>
    public bool Pass(string matchId, string callerId)
    {
        if (db != null)
        {
            var record = db.Matches.FirstOrDefault(x => x.Id == matchId);
            if (record == null || record.UserId != callerId)
            {
                return false;
            }

            record.PassedAt = DateTimeOffset.UtcNow;
            db.SaveChanges();
            return true;
        }

        var existing = storage.ReadCollection<MatchRecord>("matches").FirstOrDefault(x => x.Id == matchId);
        if (existing == null || existing.UserId != callerId)
        {
            return false;
        }

        storage.UpdateOne<MatchRecord>("matches", matchId, match => match.PassedAt = DateTimeOffset.UtcNow);
        return true;
    }

    /// <summary>
    /// 配對成功：開聊天室並通知對方。
    ///
    /// 房間用「兩個 userId 排序後串起來」當 sourceId，與誰先按無關，
    /// 所以 ChatService.CreateRoom 的既有去重可以生效——重複呼叫、
    /// 兩邊同時按，都只會得到同一間房。
    /// 通知只發給對方：按下右滑的人自己會在畫面上看到配對成功的提示。
    ///
    /// notify 為 false 代表這是重送（使用者已經右滑過了）：房間照樣查出來給前端，
    /// 但不再發第二則通知。
    /// </summary>
    private LikeResult CompleteMatch(MatchRecord match, string callerId, string otherUserId, bool notify)
    {
        var chat = services.GetService<ChatService>();
        if (chat == null)
        {
            return new LikeResult(match, true, null);
        }

        var pairKey = PairKey(callerId, otherUserId);
        string? roomId = null;
        try
        {
            roomId = chat.CreateRoom([callerId, otherUserId], "TODAY_MATCH", pairKey).Id;
        }
        catch (Exception ex)
        {
            // 開房失敗不該讓整個右滑失敗——配對本身已經寫入，
            // 對方之後再按一次或重新進入時會補開。
            // 但一定要記下來：先前這裡是靜默吞掉，結果 source_id 超長被資料庫
            // 擋下時，前端只看到 chatRoomId 是 null，完全查不出原因。
            logger?.LogError(ex, "配對成功但開聊天室失敗 caller={CallerId} other={OtherUserId}", callerId, otherUserId);
        }

        if (notify && !string.IsNullOrWhiteSpace(otherUserId) && otherUserId != callerId)
        {
            notifications?.Add(
                otherUserId,
                "MATCH",
                "你們互相同頻了 ✨",
                "有人和你一樣按下了同頻，開始聊聊吧。",
                "chat",
                roomId);
        }

        return new LikeResult(match, true, roomId);
    }

    /// <summary>
    /// 配對雙方的穩定識別碼，當作聊天室的 sourceId。
    ///
    /// 必須與「誰先按」無關，ChatService.CreateRoom 的去重才會生效。
    /// 不能直接把兩個 userId 串起來——chat_rooms.source_id 是 varchar(64)，
    /// 而 userId 本身就有 37 個字元，兩個串起來 75 字元會被資料庫擋下
    /// （第一次就是這樣失敗的，而且當時被 catch 吞掉、查不出原因）。
    /// 改成排序後取 SHA-256 的前 32 個十六進位字元，長度固定 37。
    /// </summary>
    private static string PairKey(string a, string b)
    {
        var ordered = string.Join("|", new[] { a, b }.Order());
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ordered));
        return "pair_" + Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    /// <summary>
    /// 候選池的來源。只取 ACTIVE——這在候選池改成從 users 出發之後變成必要條件：
    /// 先前池子是 today_entries，已註銷的人多半沒發過今日所以碰巧看不到；
    /// 現在每個人都在池子裡，不擋狀態的話註銷與停權帳號會全部冒出來，
    /// 還能被右滑配對成功、自動開聊天室、收到通知。
    ///
    /// ACTIVE 也是全站對「這個帳號可用」的一致定義（見 AccountStateService.IsActiveAsync）。
    /// 停權到期不會自動翻回 ACTIVE，所以到期前連登入都不行——配對照同一條線走就對齊了。
    /// </summary>
    private List<User> LoadUsers()
    {
        var active = AccountStatus.ACTIVE.ToString();

        if (db != null)
        {
            return db.Users.AsNoTracking()
                .Where(x => x.Status == active)
                .ToList()
                .Select(x => x.ToDomain())
                .ToList();
        }

        return storage.ReadCollection<User>("users")
            .Where(x => x.Status == AccountStatus.ACTIVE)
            .ToList();
    }

    private List<TodayEntry> LoadMatchableEntries()
    {
        if (db != null)
        {
            return db.TodayEntries.AsNoTracking()
                .Where(x => x.Visibility != Visibility.PRIVATE.ToString())
                .ToList()
                .Select(x => x.ToDomain())
                .ToList();
        }

        return storage.ReadCollection<TodayEntry>("todayEntries")
            .Where(x => x.Visibility != Visibility.PRIVATE)
            .ToList();
    }

    /// <summary>
    /// 一位候選的算分結果。純計算、不碰資料庫——排序與 Take 都在這個階段完成，
    /// 只有真的要發出去的那幾張才會落地。
    /// </summary>
    /// <summary>
    /// 一位候選的算分結果。純計算、不碰資料庫——排序與 Take 都在這個階段完成，
    /// 只有真的要發出去的那幾張才會落地。
    /// Entry 可能為 null：沒發過今日的人一樣要能被配到，那時只剩個人資料可比。
    /// </summary>
    private sealed record Candidate(
        string UserId, TodayEntry? Entry, int Score, MatchType Type, List<string> SharedTags);

    /// <summary>
    /// 算分素材。今日有填就用今日的，沒填就退回個人資料裡的長期偏好，
    /// 兩邊欄位對得上，下游就不必為「有沒有今日」分兩套邏輯。
    /// </summary>
    private readonly record struct Traits(
        EventType? Event,
        List<string> Emotions,
        List<string> Values,
        List<string> Interests,
        ResponseMode Response);

    private static Traits TraitsOf(TodayEntry? entry, User? user)
    {
        if (entry != null)
        {
            return new Traits(
                entry.EventType,
                entry.EmotionTags.Select(x => x.ToString()).ToList(),
                entry.ValueTags,
                entry.InterestTags,
                entry.ResponseMode);
        }

        // 事件與情緒講的是「今天發生什麼」，沒填就是沒有，不拿長期標籤硬湊——
        // 湊出來的「你們今天都遇到…」是假的，使用者一眼就看得出來。
        // 興趣與價值觀本來就是長期的，個人資料有就拿來用。
        return new Traits(
            null,
            [],
            user?.ValueTags ?? [],
            user?.InterestTags ?? [],
            user?.ResponsePreference ?? ResponseMode.JUST_LISTEN);
    }

    private static List<Candidate> RankCandidates(
        List<User> pool, TodayEntry? current, Dictionary<string, TodayEntry> latestEntries, User? me)
    {
        var mine = TraitsOf(current, me);

        return pool
            .Where(candidate => MatchesPreferences(me, candidate))
            .Select(candidate =>
            {
                latestEntries.TryGetValue(candidate.Id, out var entry);
                return ScoreCandidate(mine, candidate, entry);
            })
            .OrderByDescending(x => x.Score)
            .ToList();
    }

    private static Candidate ScoreCandidate(Traits mine, User other, TodayEntry? otherEntry)
    {
        var theirs = TraitsOf(otherEntry, other);
        var sharedTags = new List<string>();
        var score = 0;

        // 兩邊都要有今日才談得上「同一種事件」。少了 null 判斷，
        // 兩個都沒發今日的人會被當成事件相同而加 30 分。
        //
        // RANDOM 也要排除：那是 TodayAnalyzerService 認不出事件類型時的預設值
        // （見 ResolveEventType 最後一行），意思是「不知道發生什麼」，不是
        // 「我們遇到同一件事」。只選心情卡、沒打字的人全都會落在 RANDOM，
        // 算成共同事件的話，空白的今日會因為 +30 排到整副牌最前面。
        if (mine.Event != null && mine.Event != EventType.RANDOM && mine.Event == theirs.Event)
        {
            score += 30;
            sharedTags.Add(mine.Event.Value.ToString());
        }

        var emotionIntersection = mine.Emotions.Intersect(theirs.Emotions).ToList();
        score += emotionIntersection.Count * 10;
        sharedTags.AddRange(emotionIntersection);

        var valueIntersection = mine.Values.Intersect(theirs.Values).ToList();
        score += valueIntersection.Count * 10;
        sharedTags.AddRange(valueIntersection);

        var interestIntersection = mine.Interests.Intersect(theirs.Interests).ToList();
        score += interestIntersection.Count * 5;
        sharedTags.AddRange(interestIntersection);

        if (IsCompatible(mine.Response, theirs.Response))
        {
            score += 15;
            sharedTags.Add("RESPONSE_COMPATIBLE");
        }

        var matchType = ResolveMatchType(mine, theirs, emotionIntersection, interestIntersection);
        return new Candidate(other.Id, otherEntry, score, matchType, sharedTags.Distinct().ToList());
    }

    /// <summary>
    /// 把算好分的一副牌落地，並組成前端要的 MatchResponse。
    ///
    /// 回傳的每一個 MatchId 都保證是資料庫裡真實存在的列。這點很重要：
    /// 落地衝突時若把沒寫進去的 Id 當成有效值回傳，使用者後續的 like/pass
    /// 會全部 404，表態靜默遺失、畫面上卻毫無異狀。
    /// </summary>
    private List<MatchResponse> MaterializeDeck(
        string userId, TodayEntry? current, List<Candidate> deck, List<User> users)
    {
        if (deck.Count == 0) return [];

        var ids = PersistDeck(userId, current, deck);

        var responses = new List<MatchResponse>(deck.Count);
        foreach (var candidate in deck)
        {
            // 落地衝突時這副牌可能少幾張。少發幾張卡沒關係，發出死掉的 matchId 才嚴重。
            if (!ids.TryGetValue(candidate.UserId, out var matchId)) continue;

            var user = users.FirstOrDefault(x => x.Id == candidate.UserId);
            responses.Add(new MatchResponse(
                matchId,
                candidate.UserId,
                user?.Nickname ?? "同頻使用者",
                candidate.Score,
                candidate.Type,
                candidate.SharedTags,
                BuildReason(current, candidate.Type),
                BuildIcebreaker(current, candidate.Type),
                // 對方有發今日就把內容放上卡片，沒發就留空字串。
                // 卡片其餘欄位（照片、自我介紹、興趣、個性）全部來自個人資料，
                // 所以少了今日不會開天窗，只是少一段當天的話。
                candidate.Entry == null ? string.Empty : Summarize(candidate.Entry.Content),
                AgeOf(user?.Birthday),
                user?.LocationArea,
                user?.Bio,
                user?.InterestTags ?? [],
                user?.PhotoDataUrls ?? [],
                user?.Birthday?.ToString("yyyy-MM-dd"),
                user?.PersonalityTags ?? [],
                user?.AppearanceTags ?? [],
                user?.Relationship));
        }

        return responses;
    }

    /// <summary>
    /// 把一副牌裡還沒有配對列的對象寫進資料庫，回傳「對象 → matchId」。
    ///
    /// 先前是每位候選各自 SaveChanges（N 次往返）。改成批次之後要注意：
    /// EF 的 SaveChanges 是單一 transaction，只要有一列撞到
    /// ux_matches_user_matched 就整批 rollback，所以結尾一律以資料庫實際的列為準
    /// 重讀一次，不拿記憶體裡那些可能沒落地的 Id。
    /// 同一人同時打兩次 today（StrictMode、冷啟動重試、按「套用並重新配對」）
    /// 會真的撞到。
    /// </summary>
    private Dictionary<string, string> PersistDeck(string userId, TodayEntry? current, List<Candidate> deck)
    {
        var wanted = deck.Select(x => x.UserId).ToList();

        if (db != null)
        {
            var existing = db.Matches.AsNoTracking()
                .Where(x => x.UserId == userId && wanted.Contains(x.MatchedUserId))
                .Select(x => x.MatchedUserId)
                .ToHashSet();

            var added = new List<MatchRecordEntity>();
            foreach (var candidate in deck)
            {
                if (existing.Contains(candidate.UserId)) continue;
                var entity = NewMatch(userId, current, candidate).ToRecord();
                db.Matches.Add(entity);
                added.Add(entity);
            }

            if (added.Count > 0)
            {
                try
                {
                    db.SaveChanges();
                }
                catch (DbUpdateException ex)
                {
                    logger?.LogWarning(ex, "配對候選落地衝突，改用資料庫既有列 userId={UserId}", userId);
                    foreach (var entity in added)
                    {
                        db.Entry(entity).State = EntityState.Detached;
                    }
                }
            }

            return db.Matches.AsNoTracking()
                .Where(x => x.UserId == userId && wanted.Contains(x.MatchedUserId))
                .ToDictionary(x => x.MatchedUserId, x => x.Id);
        }

        var stored = storage.ReadCollection<MatchRecord>("matches")
            .Where(x => x.UserId == userId && wanted.Contains(x.MatchedUserId))
            .ToDictionary(x => x.MatchedUserId, x => x.Id);

        foreach (var candidate in deck)
        {
            if (stored.ContainsKey(candidate.UserId)) continue;
            var match = NewMatch(userId, current, candidate);
            storage.InsertOne("matches", match);
            stored[candidate.UserId] = match.Id;
        }

        return stored;
    }

    private static MatchRecord NewMatch(string userId, TodayEntry? current, Candidate candidate)
    {
        return new MatchRecord
        {
            Id = $"match_{Guid.NewGuid():N}",
            UserId = userId,
            MatchedUserId = candidate.UserId,
            MatchScore = candidate.Score,
            MatchType = candidate.Type,
            SharedTags = candidate.SharedTags,
            Reason = BuildReason(current, candidate.Type),
            Icebreaker = BuildIcebreaker(current, candidate.Type)
        };
    }

    /// <summary>
    /// 候選池要排除的對象：已表態過的人，加上封鎖關係的兩個方向。
    ///
    /// 右滑（UserLiked）是永久表態；左滑（PassedAt）只排除冷卻期內——
    /// 對方的「今天」會變，配對理由跟著變，而且候選池小的時候這是唯一的回血來源。
    /// </summary>
    private HashSet<string> LoadExcludedUserIds(string userId)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-PassCooldownDays);

        if (db != null)
        {
            var decided = db.Matches.AsNoTracking()
                .Where(x => x.UserId == userId && (x.UserLiked || (x.PassedAt != null && x.PassedAt > cutoff)))
                .Select(x => x.MatchedUserId)
                .ToHashSet();

            // 封鎖是雙向遮蔽：我封鎖的人、以及封鎖我的人，都不該再配到。
            decided.UnionWith(db.UserBlocks.AsNoTracking()
                .Where(x => x.BlockerId == userId || x.BlockedId == userId)
                .Select(x => x.BlockerId == userId ? x.BlockedId : x.BlockerId));

            return decided;
        }

        return storage.ReadCollection<MatchRecord>("matches")
            .Where(x => x.UserId == userId && (x.UserLiked || (x.PassedAt != null && x.PassedAt > cutoff)))
            .Select(x => x.MatchedUserId)
            .ToHashSet();
    }

    private static bool IsCompatible(ResponseMode a, ResponseMode b)
    {
        return a == b || a == ResponseMode.JUST_LISTEN || b == ResponseMode.JUST_LISTEN;
    }

    private static MatchType ResolveMatchType(Traits mine, Traits theirs, List<string> emotions, List<string> interests)
    {
        if (mine.Event != null && mine.Event != EventType.RANDOM && mine.Event == theirs.Event)
        {
            return MatchType.SAME_EVENT;
        }

        if (interests.Count > 0)
        {
            return MatchType.SAME_INTEREST;
        }

        if (IsCompatible(mine.Response, theirs.Response))
        {
            return MatchType.COMPLEMENTARY;
        }

        if (emotions.Count > 0)
        {
            return MatchType.SAME_EMOTION;
        }

        return MatchType.TASK_COMPATIBLE;
    }

    private static string BuildReason(TodayEntry? current, MatchType type)
    {
        return type switch
        {
            // SAME_EVENT 只有在雙方都發過今日時才算得出來，所以這裡 current 必定不為 null；
            // 加上判斷是讓編譯器看得懂，不是防真的會發生的情況。
            MatchType.SAME_EVENT when current != null => $"你們今天都遇到 {current.EventType} 類型的事情，也都適合從理解開始。",
            MatchType.SAME_INTEREST => "你們今天提到相近的興趣，可以從一件小事慢慢聊起。",
            MatchType.COMPLEMENTARY => "你們期待的回應方式相容，適合低壓地互相聽聽看。",
            MatchType.SAME_EMOTION => "事件不一定相同，但你們今天的情緒很接近。",
            _ => "你們適合同步完成一個小任務，用低壓方式慢慢認識。"
        };
    }

    private static string BuildIcebreaker(TodayEntry? current, MatchType type)
    {
        return type switch
        {
            MatchType.SAME_EVENT when current?.EventType == EventType.WORK_STRESS => "你今天也是那種明明很努力，卻被誤會的感覺嗎？",
            MatchType.SAME_EMOTION => "你今天也有那種說不上來，但很想有人懂的感覺嗎？",
            MatchType.SAME_INTEREST => "你今天提到的興趣我也有感，要不要先從一件小事聊起？",
            _ => "如果今天不用急著聊天，要不要先一起完成一個小任務？"
        };
    }

    private static string Summarize(string content)
    {
        return content.Length <= 42 ? content : $"{content[..42]}...";
    }

    private static List<MatchResponse> ApplyDisplayScore(List<MatchResponse> matches)
    {
        if (matches.Count == 0) return matches;

        var maxScore = matches.Max(x => x.MatchScore);
        var minScore = matches.Min(x => x.MatchScore);
        var range = maxScore == minScore ? 1 : maxScore - minScore;

        return matches.Select(m =>
        {
            var normalized = (double)(m.MatchScore - minScore) / range;
            var display = (int)Math.Round(80 + normalized * 18);
            return m with { MatchScore = display };
        }).ToList();
    }
}

public sealed record MatchResponse(
    string MatchId,
    string MatchedUserId,
    string Nickname,
    int MatchScore,
    MatchType MatchType,
    List<string> SharedTags,
    string Reason,
    string Icebreaker,
    string TodaySummary,
    int? Age = null,
    string? City = null,
    string? Bio = null,
    List<string>? Interests = null,
    List<string>? Photos = null,
    string? Birthday = null,
    // 個性／外貌／感情狀態：使用者在個人檔案填了，但配對卡原本沒帶，
    // 所以別人看到的卡片永遠只有興趣。新參數一律放尾端並給預設值，
    // 既有的具名與位置呼叫都不受影響。
    List<string>? PersonalityTags = null,
    List<string>? AppearanceTags = null,
    string? Relationship = null);

/// <summary>
/// 右滑的結果。IsMutual 為 true 時代表兩邊都按了同頻，ChatRoomId 是自動
/// 開好的房間（開房失敗時為 null，前端就只顯示配對成功、不給進聊天的按鈕）。
/// </summary>
public sealed record LikeResult(MatchRecord Match, bool IsMutual, string? ChatRoomId);
