using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Infrastructure.Persistence.Configurations;

public sealed class AuthAccountRecordConfiguration : IEntityTypeConfiguration<AuthAccountRecord>
{
    public void Configure(EntityTypeBuilder<AuthAccountRecord> builder)
    {
        builder.ToTable("auth_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(120).IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
        builder.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(x => x.IsDisabled).HasColumnName("is_disabled");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(x => x.Username).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

public sealed class PasswordResetTokenRecordConfiguration : IEntityTypeConfiguration<PasswordResetTokenRecord>
{
    public void Configure(EntityTypeBuilder<PasswordResetTokenRecord> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CodeHash).HasColumnName("code_hash").HasMaxLength(500).IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.UsedAt).HasColumnName("used_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt });
    }
}

public sealed class RegistrationVerificationTokenRecordConfiguration : IEntityTypeConfiguration<RegistrationVerificationTokenRecord>
{
    public void Configure(EntityTypeBuilder<RegistrationVerificationTokenRecord> builder)
    {
        builder.ToTable("registration_verification_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Nickname).HasColumnName("nickname").HasMaxLength(80).IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
        builder.Property(x => x.BirthYear).HasColumnName("birth_year").HasMaxLength(40);
        builder.Property(x => x.Gender).HasColumnName("gender").HasMaxLength(40);
        builder.Property(x => x.CodeHash).HasColumnName("code_hash").HasMaxLength(500).IsRequired();
        builder.Property(x => x.TermsAcceptedAt).HasColumnName("terms_accepted_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.UsedAt).HasColumnName("used_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.Email, x.ExpiresAt });
    }
}
