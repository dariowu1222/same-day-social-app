using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Infrastructure.Persistence.Configurations;

public sealed class AdminLoginCodeRecordConfiguration : IEntityTypeConfiguration<AdminLoginCodeRecord>
{
    public void Configure(EntityTypeBuilder<AdminLoginCodeRecord> builder)
    {
        builder.ToTable("admin_login_codes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CodeHash).HasColumnName("code_hash").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Attempts).HasColumnName("attempts");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.UsedAt).HasColumnName("used_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        // 驗證時一律只看「這個人最新的那一筆」，所以照 user + 時間查
        builder.HasIndex(x => new { x.UserId, x.Purpose, x.CreatedAt });
    }
}

public sealed class AdminSessionRecordConfiguration : IEntityTypeConfiguration<AdminSessionRecord>
{
    public void Configure(EntityTypeBuilder<AdminSessionRecord> builder)
    {
        builder.ToTable("admin_sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(x => x.LastVerifiedAt).HasColumnName("last_verified_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.CreatedIp).HasColumnName("created_ip").HasMaxLength(64);
        builder.Property(x => x.CreatedUserAgent).HasColumnName("created_user_agent").HasMaxLength(300);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => x.UserId);
    }
}
