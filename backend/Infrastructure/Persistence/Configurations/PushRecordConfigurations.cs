using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Infrastructure.Persistence.Configurations;

public sealed class DeviceTokenRecordConfiguration : IEntityTypeConfiguration<DeviceTokenRecord>
{
    public void Configure(EntityTypeBuilder<DeviceTokenRecord> builder)
    {
        builder.ToTable("device_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Token).HasColumnName("token").HasMaxLength(512).IsRequired();
        builder.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(20);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

public sealed class PushDigestStateRecordConfiguration : IEntityTypeConfiguration<PushDigestStateRecord>
{
    public void Configure(EntityTypeBuilder<PushDigestStateRecord> builder)
    {
        builder.ToTable("push_digest_states");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64);
        builder.Property(x => x.LastEventAt).HasColumnName("last_event_at");
        builder.Property(x => x.LastPushedAt).HasColumnName("last_pushed_at");
    }
}
