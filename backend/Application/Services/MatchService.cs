using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using MatchType = SameDaySocialApp.Domain.Enums.MatchType;

namespace SameDaySocialApp.Application.Services;

public sealed class MatchService(JsonStorageService storage)
{
    public List<MatchResponse> GetTodayMatches(string userId)
    {
        var users = storage.ReadCollection<User>("users");
        var entries = storage.ReadCollection<TodayEntry>("todayEntries")
            .Where(x => x.Visibility != Visibility.PRIVATE)
            .ToList();
        var current = entries.Where(x => x.UserId == userId).MaxBy(x => x.CreatedAt);
        if (current == null)
        {
            return [];
        }

        return entries
            .Where(x => x.UserId != userId)
            .GroupBy(x => x.UserId)
            .Select(g => g.MaxBy(x => x.CreatedAt)!)
            .Select(entry => BuildMatch(current, entry, users))
            .Where(x => x.MatchScore >= 40)
            .OrderByDescending(x => x.MatchScore)
            .ToList();
    }

    public MatchRecord? Like(string matchId)
    {
        return storage.UpdateOne<MatchRecord>("matches", matchId, match => match.UserLiked = true);
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
            Reason = BuildReason(current, other, matchType),
            Icebreaker = BuildIcebreaker(current, matchType)
        };

        var existing = storage.ReadCollection<MatchRecord>("matches")
            .FirstOrDefault(x => x.UserId == match.UserId && x.MatchedUserId == match.MatchedUserId);
        if (existing == null)
        {
            storage.InsertOne("matches", match);
        }
        else
        {
            match.Id = existing.Id;
            match.UserLiked = existing.UserLiked;
            match.MatchedUserLiked = existing.MatchedUserLiked;
        }

        return new MatchResponse(
            match.Id,
            match.MatchedUserId,
            user?.Nickname ?? "同頻的人",
            match.MatchScore,
            match.MatchType,
            match.SharedTags,
            match.Reason,
            match.Icebreaker,
            Summarize(other.Content));
    }

    private static bool IsCompatible(ResponseMode a, ResponseMode b)
    {
        return a == b || a == ResponseMode.JUST_LISTEN || b == ResponseMode.JUST_LISTEN;
    }

    private static MatchType ResolveMatchType(TodayEntry current, TodayEntry other, List<string> emotions, List<string> interests)
    {
        if (current.EventType == other.EventType) return MatchType.SAME_EVENT;
        if (interests.Count > 0) return MatchType.SAME_INTEREST;
        if (IsCompatible(current.ResponseMode, other.ResponseMode)) return MatchType.COMPLEMENTARY;
        if (emotions.Count > 0) return MatchType.SAME_EMOTION;
        return MatchType.TASK_COMPATIBLE;
    }

    private static string BuildReason(TodayEntry current, TodayEntry other, MatchType type)
    {
        return type switch
        {
            MatchType.SAME_EVENT => $"你們今天都遇到「{current.EventType}」相關的事，也許能理解彼此的卡住。",
            MatchType.SAME_INTEREST => "你們今天提到相近的興趣，可以用低壓的小話題開始。",
            MatchType.COMPLEMENTARY => "你們期待的回應方式相容，適合慢慢聊。",
            MatchType.SAME_EMOTION => "雖然事情不同，但你們今天的情緒很接近。",
            _ => "你們適合先從一個小任務開始，不急著聊天。"
        };
    }

    private static string BuildIcebreaker(TodayEntry current, MatchType type)
    {
        return type switch
        {
            MatchType.SAME_EVENT when current.EventType == EventType.WORK_STRESS => "你今天也是那種明明很努力，卻被誤會的感覺嗎？",
            MatchType.SAME_EMOTION => "你今天的那種感覺，是比較想被聽見，還是想先放空一下？",
            MatchType.SAME_INTEREST => "你今天提到的那件小事，我也有點懂，可以聊聊嗎？",
            _ => "如果不急著聊天，我們可以先一起完成一個小任務。"
        };
    }

    private static string Summarize(string content)
    {
        return content.Length <= 42 ? content : $"{content[..42]}...";
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
