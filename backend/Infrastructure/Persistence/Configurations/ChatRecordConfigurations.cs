using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Infrastructure.Persistence.Configurations;

public sealed class ChatRoomRecordConfiguration : IEntityTypeConfiguration<ChatRoomRecord>
{
    public void Configure(EntityTypeBuilder<ChatRoomRecord> builder)
    {
        builder.ToTable("chat_rooms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(40);
        builder.Property(x => x.SourceId).HasColumnName("source_id").HasMaxLength(64);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.SourceType, x.SourceId });
    }
}

public sealed class ChatRoomUserRecordConfiguration : IEntityTypeConfiguration<ChatRoomUserRecord>
{
    public void Configure(EntityTypeBuilder<ChatRoomUserRecord> builder)
    {
        builder.ToTable("chat_room_users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.ChatRoomId).HasColumnName("chat_room_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.JoinedAt).HasColumnName("joined_at");
        builder.HasIndex(x => new { x.ChatRoomId, x.UserId }).IsUnique();
    }
}

public sealed class ChatMessageRecordConfiguration : IEntityTypeConfiguration<ChatMessageRecord>
{
    public void Configure(EntityTypeBuilder<ChatMessageRecord> builder)
    {
        builder.ToTable("chat_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.ChatRoomId).HasColumnName("chat_room_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.SenderId).HasColumnName("sender_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.ChatRoomId, x.CreatedAt });
    }
}
