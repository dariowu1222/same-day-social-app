using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Infrastructure.Persistence.Configurations;

public sealed class UserRecordConfiguration : IEntityTypeConfiguration<UserRecord>
{
    public void Configure(EntityTypeBuilder<UserRecord> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.Nickname).HasColumnName("nickname").HasMaxLength(80).IsRequired();
        builder.Property(x => x.AgeRange).HasColumnName("age_range").HasMaxLength(40);
        builder.Property(x => x.Gender).HasColumnName("gender").HasMaxLength(40);
        builder.Property(x => x.LocationArea).HasColumnName("location_area").HasMaxLength(80);
        builder.Property(x => x.Bio).HasColumnName("bio").HasMaxLength(500);
        builder.Property(x => x.InterestTags).HasColumnName("interest_tags").HasColumnType("text[]");
        builder.Property(x => x.ValueTags).HasColumnName("value_tags").HasColumnType("text[]");
        builder.Property(x => x.ResponsePreference).HasColumnName("response_preference").HasMaxLength(40);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.PhotoDataUrls).HasColumnName("photo_data_urls").HasColumnType("text[]");
        builder.Property(x => x.Birthday).HasColumnName("birthday").HasColumnType("date");
    }
}
