using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Application.Services;

public sealed class TodayEntryService
{
    private readonly JsonStorageService storage;
    private readonly TodayAnalyzerService analyzer;
    private readonly AppDbContext? db;

    public TodayEntryService(JsonStorageService storage, TodayAnalyzerService analyzer, IServiceProvider services)
    {
        this.storage = storage;
        this.analyzer = analyzer;
        db = services.GetService<AppDbContext>();
    }

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

        if (db != null)
        {
            db.TodayEntries.Add(entry.ToRecord());
            db.SaveChanges();
            return (entry, analysis);
        }

        storage.InsertOne("todayEntries", entry);
        return (entry, analysis);
    }

    public List<TodayEntry> GetByUser(string userId)
    {
        if (db != null)
        {
            return db.TodayEntries
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToList()
                .Select(x => x.ToDomain())
                .ToList();
        }

        return storage.ReadCollection<TodayEntry>("todayEntries")
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }
}
