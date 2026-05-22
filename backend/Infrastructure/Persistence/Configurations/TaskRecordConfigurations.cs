using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Infrastructure.Persistence.Configurations;

public sealed class SocialTaskRecordConfiguration : IEntityTypeConfiguration<SocialTaskRecord>
{
    public void Configure(EntityTypeBuilder<SocialTaskRecord> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(40);
        builder.Property(x => x.Duration).HasColumnName("duration").HasMaxLength(80);
        builder.Property(x => x.Difficulty).HasColumnName("difficulty").HasMaxLength(40);
        builder.Property(x => x.ParticipantLimit).HasColumnName("participant_limit");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => x.Category);
    }
}

public sealed class TaskParticipantRecordConfiguration : IEntityTypeConfiguration<TaskParticipantRecord>
{
    public void Configure(EntityTypeBuilder<TaskParticipantRecord> builder)
    {
        builder.ToTable("task_participants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.TaskId).HasColumnName("task_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.JoinedAt).HasColumnName("joined_at");
        builder.HasIndex(x => new { x.TaskId, x.UserId }).IsUnique();
    }
}
