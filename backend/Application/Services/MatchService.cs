using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using MatchType = SameDaySocialApp.Domain.Enums.MatchType;

namespace SameDaySocialApp.Application.Services;

public sealed class MatchService
{
    private readonly JsonStorageService storage;
    private readonly AppDbContext? db;
    private readonly NotificationService? notifications;

    public MatchService(JsonStorageService storage, IServiceProvider services)
    {
        this.storage = storage;
        db = services.GetService<AppDbContext>();
        notifications = services.GetService<NotificationService>();
    }

    public List<MatchResponse> GetTodayMatches(string userId)
    {
        var users = LoadUsers();
        var entries = LoadMatchableEntries();
        var current = entries.Where(x => x.UserId == userId).MaxBy(x => x.CreatedAt);
        if (current == null)
        {
            return [];
        }

        // 暫停配對：本人暫停則不配對；其他暫停者也不出現在候選
        var pausedUserIds = LoadPausedUserIds();
        if (pausedUserIds.Contains(userId))
        {
            return [];
        }

        var matches = entries
            .Where(x => x.UserId != userId && !pausedUserIds.Contains(x.UserId))
            .GroupBy(x => x.UserId)
            .Select(g => g.MaxBy(x => x.CreatedAt)!)
            .Select(entry => BuildMatch(current, entry, users))
            .OrderByDescending(x => x.MatchScore)
            .Take(5)
            .ToList();

        return ApplyDisplayScore(matches);
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

    public MatchRecord? Like(string matchId, string callerId)
    {
        if (db != null)
        {
            var record = db.Matches.FirstOrDefault(x => x.Id == matchId);
            // 所有權驗證：這筆配對必須屬於目前登入者
            if (record == null || record.UserId != callerId)
            {
                return null;
            }

            record.UserLiked = true;
            db.SaveChanges();
            NotifyLiked(record.MatchedUserId, callerId);
            return record.ToDomain();
        }

        var existing = storage.ReadCollection<MatchRecord>("matches").FirstOrDefault(x => x.Id == matchId);
        if (existing == null || existing.UserId != callerId)
        {
            return null;
        }
        var liked = storage.UpdateOne<MatchRecord>("matches", matchId, match => match.UserLiked = true);
        NotifyLiked(existing.MatchedUserId, callerId);
        return liked;
    }

    private void NotifyLiked(string recipientId, string callerId)
    {
        if (string.IsNullOrWhiteSpace(recipientId) || recipientId == callerId) return;
        notifications?.Add(
            recipientId,
            "LIKE",
            "有人對你有共鳴 💞",
            "有人喜歡了你的今日，去看看你的同頻配對吧。",
            "match");
    }

    private List<User> LoadUsers()
    {
        if (db != null)
        {
            return db.Users.AsNoTracking()
                .ToList()
                .Select(x => x.ToDomain())
                .ToList();
        }

        return storage.ReadCollection<User>("users");
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

    private MatchResponse BuildMatch(TodayEntry current, TodayEntry other, List<User> users)
    {
        var sharedTags = new List<string>();
        var score = 0;

        if (current.EventType == other.EventType)
        {
            score += 30;
            sharedTags.Add(current.EventType.ToString());
        }

        var emotionIntersection = current.EmotionTags.Intersect(other.EmotionTags).Select(x => x.ToString()).ToList();
        score += emotionIntersection.Count * 10;
        sharedTags.AddRange(emotionIntersection);

        var valueIntersection = current.ValueTags.Intersect(other.ValueTags).ToList();
        score += valueIntersection.Count * 10;
        sharedTags.AddRange(valueIntersection);

        var interestIntersection = current.InterestTags.Intersect(other.InterestTags).ToList();
        score += interestIntersection.Count * 5;
        sharedTags.AddRange(interestIntersection);

        if (IsCompatible(current.ResponseMode, other.ResponseMode))
        {
            score += 15;
            sharedTags.Add("RESPONSE_COMPATIBLE");
        }

        var matchType = ResolveMatchType(current, other, emotionIntersection, interestIntersection);
        var user = users.FirstOrDefault(x => x.Id == other.UserId);

        var match = new MatchRecord
        {
            Id = $"match_{Guid.NewGuid():N}",
            UserId = current.UserId,
            MatchedUserId = other.UserId,
            MatchScore = score,
            MatchType = matchType,
            SharedTags = sharedTags.Distinct().ToList(),
            Reason = BuildReason(current, matchType),
            Icebreaker = BuildIcebreaker(current, matchType)
        };

        PersistOrReuseMatch(match);

        return new MatchResponse(
            match.Id,
            match.MatchedUserId,
            user?.Nickname ?? "同頻使用者",
            match.MatchScore,
            match.MatchType,
            match.SharedTags,
            match.Reason,
            match.Icebreaker,
            Summarize(other.Content));
    }

    private void PersistOrReuseMatch(MatchRecord match)
    {
        if (db != null)
        {
            var existing = db.Matches.FirstOrDefault(x => x.UserId == match.UserId && x.MatchedUserId == match.MatchedUserId);
            if (existing == null)
            {
                db.Matches.Add(match.ToRecord());
                db.SaveChanges();
                return;
            }

            match.Id = existing.Id;
            match.UserLiked = existing.UserLiked;
            match.MatchedUserLiked = existing.MatchedUserLiked;
            return;
        }

        var existingFileMatch = storage.ReadCollection<MatchRecord>("matches")
            .FirstOrDefault(x => x.UserId == match.UserId && x.MatchedUserId == match.MatchedUserId);
        if (existingFileMatch == null)
        {
            storage.InsertOne("matches", match);
            return;
        }

        match.Id = existingFileMatch.Id;
        match.UserLiked = existingFileMatch.UserLiked;
        match.MatchedUserLiked = existingFileMatch.MatchedUserLiked;
    }

    private static bool IsCompatible(ResponseMode a, ResponseMode b)
    {
        return a == b || a == ResponseMode.JUST_LISTEN || b == ResponseMode.JUST_LISTEN;
    }

    private static MatchType ResolveMatchType(TodayEntry current, TodayEntry other, List<string> emotions, List<string> interests)
    {
        if (current.EventType == other.EventType)
        {
            return MatchType.SAME_EVENT;
        }

        if (interests.Count > 0)
        {
            return MatchType.SAME_INTEREST;
        }

        if (IsCompatible(current.ResponseMode, other.ResponseMode))
        {
            return MatchType.COMPLEMENTARY;
        }

        if (emotions.Count > 0)
        {
            return MatchType.SAME_EMOTION;
        }

        return MatchType.TASK_COMPATIBLE;
    }

    private static string BuildReason(TodayEntry current, MatchType type)
    {
        return type switch
        {
            MatchType.SAME_EVENT => $"你們今天都遇到 {current.EventType} 類型的事情，也都適合從理解開始。",
            MatchType.SAME_INTEREST => "你們今天提到相近的興趣，可以從一件小事慢慢聊起。",
            MatchType.COMPLEMENTARY => "你們期待的回應方式相容，適合低壓地互相聽聽看。",
            MatchType.SAME_EMOTION => "事件不一定相同，但你們今天的情緒很接近。",
            _ => "你們適合同步完成一個小任務，用低壓方式慢慢認識。"
        };
    }

    private static string BuildIcebreaker(TodayEntry current, MatchType type)
    {
        return type switch
        {
            MatchType.SAME_EVENT when current.EventType == EventType.WORK_STRESS => "你今天也是那種明明很努力，卻被誤會的感覺嗎？",
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
    string TodaySummary);
