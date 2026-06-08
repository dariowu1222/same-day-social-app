using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Infrastructure.Persistence.Configurations;

public sealed class RantPostRecordConfiguration : IEntityTypeConfiguration<RantPostRecord>
{
    public void Configure(EntityTypeBuilder<RantPostRecord> builder)
    {
        builder.ToTable("rant_posts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Nickname).HasColumnName("nickname").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").HasMaxLength(3000).IsRequired();
        builder.Property(x => x.Mode).HasColumnName("mode").HasMaxLength(40);
        builder.Property(x => x.EmotionTags).HasColumnName("emotion_tags").HasColumnType("text[]");
        builder.Property(x => x.HashTags).HasColumnName("hash_tags").HasColumnType("text[]");
        builder.Property(x => x.ImageDataUrl).HasColumnName("image_data_url");
        builder.Property(x => x.AudioDataUrl).HasColumnName("audio_data_url");
        builder.Property(x => x.IsHidden).HasColumnName("is_hidden");
        builder.Property(x => x.ReportCount).HasColumnName("report_count");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.IsHidden, x.CreatedAt });
    }
}

public sealed class RantReplyRecordConfiguration : IEntityTypeConfiguration<RantReplyRecord>
{
    public void Configure(EntityTypeBuilder<RantReplyRecord> builder)
    {
        builder.ToTable("rant_replies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.RantPostId).HasColumnName("rant_post_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ParentReplyId).HasColumnName("parent_reply_id").HasMaxLength(64);
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Nickname).HasColumnName("nickname").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ImageDataUrl).HasColumnName("image_data_url");
        builder.Property(x => x.AudioDataUrl).HasColumnName("audio_data_url");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.RantPostId, x.CreatedAt });
        builder.HasIndex(x => x.ParentReplyId);
    }
}

public sealed class RantReactionRecordConfiguration : IEntityTypeConfiguration<RantReactionRecord>
{
    public void Configure(EntityTypeBuilder<RantReactionRecord> builder)
    {
        builder.ToTable("rant_reactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.RantPostId).HasColumnName("rant_post_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ReactionType).HasColumnName("reaction_type").HasMaxLength(40);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.RantPostId, x.UserId, x.ReactionType }).IsUnique();
    }
}

public sealed class RantReportRecordConfiguration : IEntityTypeConfiguration<RantReportRecord>
{
    public void Configure(EntityTypeBuilder<RantReportRecord> builder)
    {
        builder.ToTable("rant_reports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.RantPostId).HasColumnName("rant_post_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.RantPostId, x.UserId }).IsUnique();
    }
}
