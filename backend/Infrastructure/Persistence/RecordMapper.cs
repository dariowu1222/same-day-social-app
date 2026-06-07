using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using MatchType = SameDaySocialApp.Domain.Enums.MatchType;

namespace SameDaySocialApp.Infrastructure.Persistence;

public static class RecordMapper
{
    public static User ToDomain(this UserRecord record)
    {
        return new User
        {
            Id = record.Id,
            Nickname = record.Nickname,
            AgeRange = record.AgeRange,
            Gender = record.Gender,
            LocationArea = record.LocationArea,
            Bio = record.Bio,
            InterestTags = record.InterestTags.ToList(),
            ValueTags = record.ValueTags.ToList(),
            ResponsePreference = ParseEnum(record.ResponsePreference, ResponseMode.JUST_LISTEN),
            CreatedAt = record.CreatedAt
        };
    }

    public static UserRecord ToRecord(this User user)
    {
        return new UserRecord
        {
            Id = user.Id,
            Nickname = user.Nickname,
            AgeRange = user.AgeRange,
            Gender = user.Gender,
            LocationArea = user.LocationArea,
            Bio = user.Bio,
            InterestTags = user.InterestTags.ToArray(),
            ValueTags = user.ValueTags.ToArray(),
            ResponsePreference = user.ResponsePreference.ToString(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static TodayEntry ToDomain(this TodayEntryRecord record)
    {
        return new TodayEntry
        {
            Id = record.Id,
            UserId = record.UserId,
            Content = record.Content,
            EventType = ParseEnum(record.EventType, EventType.RANDOM),
            EmotionTags = record.EmotionTags.Select(x => ParseEnum(x, EmotionTag.CALM)).ToList(),
            ValueTags = record.ValueTags.ToList(),
            InterestTags = record.InterestTags.ToList(),
            ResponseMode = ParseEnum(record.ResponseMode, ResponseMode.JUST_LISTEN),
            Visibility = ParseEnum(record.Visibility, Visibility.MATCH_ONLY),
            CreatedAt = record.CreatedAt
        };
    }

    public static TodayEntryRecord ToRecord(this TodayEntry entry)
    {
        return new TodayEntryRecord
        {
            Id = entry.Id,
            UserId = entry.UserId,
            Content = entry.Content,
            EventType = entry.EventType.ToString(),
            EmotionTags = entry.EmotionTags.Select(x => x.ToString()).ToArray(),
            ValueTags = entry.ValueTags.ToArray(),
            InterestTags = entry.InterestTags.ToArray(),
            ResponseMode = entry.ResponseMode.ToString(),
            Visibility = entry.Visibility.ToString(),
            CreatedAt = entry.CreatedAt
        };
    }

    public static MatchRecord ToDomain(this MatchRecordEntity record)
    {
        return new MatchRecord
        {
            Id = record.Id,
            UserId = record.UserId,
            MatchedUserId = record.MatchedUserId,
            MatchScore = record.MatchScore,
            MatchType = ParseEnum(record.MatchType, MatchType.SAME_EMOTION),
            SharedTags = record.SharedTags.ToList(),
            Reason = record.Reason,
            Icebreaker = record.Icebreaker,
            UserLiked = record.UserLiked,
            MatchedUserLiked = record.MatchedUserLiked,
            CreatedAt = record.CreatedAt
        };
    }

    public static MatchRecordEntity ToRecord(this MatchRecord match)
    {
        return new MatchRecordEntity
        {
            Id = match.Id,
            UserId = match.UserId,
            MatchedUserId = match.MatchedUserId,
            MatchScore = match.MatchScore,
            MatchType = match.MatchType.ToString(),
            SharedTags = match.SharedTags.ToArray(),
            Reason = match.Reason,
            Icebreaker = match.Icebreaker,
            UserLiked = match.UserLiked,
            MatchedUserLiked = match.MatchedUserLiked,
            CreatedAt = match.CreatedAt
        };
    }

    public static RantPost ToDomain(this RantPostRecord record, IEnumerable<RantReplyRecord> replies, int likeCount, bool likedByMe = false)
    {
        var domainReplies = replies
            .OrderBy(x => x.CreatedAt)
            .Select(x => new RantReply
            {
                Id = x.Id,
                UserId = x.UserId,
                Nickname = x.Nickname,
                Content = x.Content,
                ParentReplyId = x.ParentReplyId,
                CreatedAt = x.CreatedAt
            })
            .ToList();

        return new RantPost
        {
            Id = record.Id,
            UserId = record.UserId,
            Nickname = record.Nickname,
            Content = record.Content,
            Mode = ParseEnum(record.Mode, RantMode.JUST_SAYING),
            EmotionTags = record.EmotionTags.Select(x => ParseEnum(x, EmotionTag.CALM)).ToList(),
            CreatedAt = record.CreatedAt,
            LikeCount = likeCount,
            LikedByMe = likedByMe,
            ReplyCount = domainReplies.Count,
            IsHidden = record.IsHidden,
            ReportCount = record.ReportCount,
            Replies = domainReplies
        };
    }

    public static RantPostRecord ToRecord(this RantPost post)
    {
        return new RantPostRecord
        {
            Id = post.Id,
            UserId = post.UserId,
            Nickname = post.Nickname,
            Content = post.Content,
            Mode = post.Mode.ToString(),
            EmotionTags = post.EmotionTags.Select(x => x.ToString()).ToArray(),
            IsHidden = post.IsHidden,
            ReportCount = post.ReportCount,
            CreatedAt = post.CreatedAt
        };
    }

    public static SocialTask ToDomain(this SocialTaskRecord record, IEnumerable<TaskParticipantRecord> participants)
    {
        return new SocialTask
        {
            Id = record.Id,
            Title = record.Title,
            Description = record.Description,
            Category = ParseEnum(record.Category, TaskCategory.WALK),
            Duration = record.Duration,
            Difficulty = record.Difficulty,
            ParticipantLimit = record.ParticipantLimit,
            ParticipantUserIds = participants.Select(x => x.UserId).Distinct().ToList(),
            CreatedAt = record.CreatedAt
        };
    }

    public static SocialTaskRecord ToRecord(this SocialTask task)
    {
        return new SocialTaskRecord
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Category = task.Category.ToString(),
            Duration = task.Duration,
            Difficulty = task.Difficulty,
            ParticipantLimit = task.ParticipantLimit,
            CreatedAt = task.CreatedAt
        };
    }

    public static ChatRoom ToDomain(this ChatRoomRecord record, IEnumerable<ChatRoomUserRecord> users)
    {
        return new ChatRoom
        {
            Id = record.Id,
            UserIds = users.Select(x => x.UserId).Distinct().ToList(),
            SourceType = record.SourceType,
            SourceId = record.SourceId,
            CreatedAt = record.CreatedAt
        };
    }

    public static ChatMessage ToDomain(this ChatMessageRecord record)
    {
        return new ChatMessage
        {
            Id = record.Id,
            ChatRoomId = record.ChatRoomId,
            SenderId = record.SenderId,
            Content = record.Content,
            CreatedAt = record.CreatedAt
        };
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
    }
}
