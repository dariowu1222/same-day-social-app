using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Application.Services;

public sealed class TodayEntryService(JsonStorageService storage, TodayAnalyzerService analyzer)
{
    public (TodayEntry Entry, TodayAnalysis Analysis) Create(string userId, string content, ResponseMode responseMode, Visibility visibility)
    {
        var analysis = analyzer.Analyze(content, responseMode);
        var entry = new TodayEntry
        {
            Id = $"today_{Guid.NewGuid():N}",
            UserId = userId,
            Content = content.Trim(),
            EventType = analysis.EventType,
            EmotionTags = analysis.EmotionTags,
            ValueTags = analysis.ValueTags,
            InterestTags = analysis.InterestTags,
            ResponseMode = analysis.ResponseMode,
            Visibility = visibility
        };

        storage.InsertOne("todayEntries", entry);
        return (entry, analysis);
    }

    public List<TodayEntry> GetByUser(string userId)
    {
        return storage.ReadCollection<TodayEntry>("todayEntries")
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }
}
