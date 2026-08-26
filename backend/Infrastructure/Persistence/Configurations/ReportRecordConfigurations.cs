using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Infrastructure.Persistence.Configurations;

public sealed class ReportRecordConfiguration : IEntityTypeConfiguration<ReportRecord>
{
    public void Configure(EntityTypeBuilder<ReportRecord> builder)
    {
        builder.ToTable("reports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(40).IsRequired();
        builder.Property(x => x.TargetId).HasColumnName("target_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ReporterId).HasColumnName("reporter_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ReportedUserId).HasColumnName("reported_user_id").HasMaxLength(64);
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Detail).HasColumnName("detail").HasMaxLength(1000);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
        builder.Property(x => x.Severity).HasColumnName("severity");
        builder.Property(x => x.ContextSnapshot).HasColumnName("context_snapshot").HasColumnType("text");
        builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by").HasMaxLength(64);
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(x => x.Resolution).HasColumnName("resolution").HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        // 審核佇列：待處理優先、嚴重度高的先看、同級距先進先出
        builder.HasIndex(x => new { x.Status, x.Severity, x.CreatedAt });
        // 同一人對同一標的只能檢舉一次（重複點不該灌高檢舉數）
        builder.HasIndex(x => new { x.TargetType, x.TargetId, x.ReporterId }).IsUnique();
        // 累犯統計
        builder.HasIndex(x => x.ReportedUserId);
    }
}

public sealed class ModerationAuditRecordConfiguration : IEntityTypeConfiguration<ModerationAuditRecord>
{
    public void Configure(EntityTypeBuilder<ModerationAuditRecord> builder)
    {
        builder.ToTable("moderation_audits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.ActorId).HasColumnName("actor_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ReportId).HasColumnName("report_id").HasMaxLength(64);
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.ActorId, x.CreatedAt });
    }
}

public sealed class AccountSanctionRecordConfiguration : IEntityTypeConfiguration<AccountSanctionRecord>
{
    public void Configure(EntityTypeBuilder<AccountSanctionRecord> builder)
    {
        builder.ToTable("account_sanctions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.ActorId).HasColumnName("actor_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ReportId).HasColumnName("report_id").HasMaxLength(64);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
