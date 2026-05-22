using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Infrastructure.Persistence.Configurations;

public sealed class TodayEntryRecordConfiguration : IEntityTypeConfiguration<TodayEntryRecord>
{
    public void Configure(EntityTypeBuilder<TodayEntryRecord> builder)
    {
        builder.ToTable("today_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(40);
        builder.Property(x => x.EmotionTags).HasColumnName("emotion_tags").HasColumnType("text[]");
        builder.Property(x => x.ValueTags).HasColumnName("value_tags").HasColumnType("text[]");
        builder.Property(x => x.InterestTags).HasColumnName("interest_tags").HasColumnType("text[]");
        builder.Property(x => x.ResponseMode).HasColumnName("response_mode").HasMaxLength(40);
        builder.Property(x => x.Visibility).HasColumnName("visibility").HasMaxLength(40);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => new { x.EventType, x.CreatedAt });
    }
}

public sealed class MatchRecordEntityConfiguration : IEntityTypeConfiguration<MatchRecordEntity>
{
    public void Configure(EntityTypeBuilder<MatchRecordEntity> builder)
    {
        builder.ToTable("matches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.MatchedUserId).HasColumnName("matched_user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.MatchScore).HasColumnName("match_score");
        builder.Property(x => x.MatchType).HasColumnName("match_type").HasMaxLength(40);
        builder.Property(x => x.SharedTags).HasColumnName("shared_tags").HasColumnType("text[]");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.Icebreaker).HasColumnName("icebreaker").HasMaxLength(500);
        builder.Property(x => x.UserLiked).HasColumnName("user_liked");
        builder.Property(x => x.MatchedUserLiked).HasColumnName("matched_user_liked");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.UserId, x.MatchedUserId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.MatchScore });
    }
}
