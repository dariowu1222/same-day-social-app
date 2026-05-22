using Microsoft.EntityFrameworkCore;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserRecord> Users => Set<UserRecord>();
    public DbSet<AuthAccountRecord> AuthAccounts => Set<AuthAccountRecord>();
    public DbSet<PasswordResetTokenRecord> PasswordResetTokens => Set<PasswordResetTokenRecord>();
    public DbSet<TodayEntryRecord> TodayEntries => Set<TodayEntryRecord>();
    public DbSet<MatchRecordEntity> Matches => Set<MatchRecordEntity>();
    public DbSet<RantPostRecord> RantPosts => Set<RantPostRecord>();
    public DbSet<RantReplyRecord> RantReplies => Set<RantReplyRecord>();
    public DbSet<RantReactionRecord> RantReactions => Set<RantReactionRecord>();
    public DbSet<RantReportRecord> RantReports => Set<RantReportRecord>();
    public DbSet<SocialTaskRecord> Tasks => Set<SocialTaskRecord>();
    public DbSet<TaskParticipantRecord> TaskParticipants => Set<TaskParticipantRecord>();
    public DbSet<ChatRoomRecord> ChatRooms => Set<ChatRoomRecord>();
    public DbSet<ChatRoomUserRecord> ChatRoomUsers => Set<ChatRoomUserRecord>();
    public DbSet<ChatMessageRecord> ChatMessages => Set<ChatMessageRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
