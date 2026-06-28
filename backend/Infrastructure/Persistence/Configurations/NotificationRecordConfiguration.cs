using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Infrastructure.Persistence.Configurations;

public sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.RecipientId).HasColumnName("recipient_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(120);
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(300);
        builder.Property(x => x.LinkType).HasColumnName("link_type").HasMaxLength(20);
        builder.Property(x => x.LinkId).HasColumnName("link_id").HasMaxLength(64);
        builder.Property(x => x.IsRead).HasColumnName("is_read").HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        // 收件匣查詢：依收件者 + 時間倒序；未讀數也走這條
        builder.HasIndex(x => new { x.RecipientId, x.CreatedAt });
    }
}
