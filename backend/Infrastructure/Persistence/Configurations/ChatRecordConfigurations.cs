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
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.QuotedMessageId).HasColumnName("quoted_message_id").HasMaxLength(64);
        builder.Property(x => x.QuotedSenderName).HasColumnName("quoted_sender_name").HasMaxLength(100);
        builder.Property(x => x.QuotedContent).HasColumnName("quoted_content").HasMaxLength(2000);
        builder.Property(x => x.IsRecalled).HasColumnName("is_recalled").HasDefaultValue(false);
        builder.HasIndex(x => new { x.ChatRoomId, x.CreatedAt });
    }
}

public sealed class ChatMemberSettingRecordConfiguration : IEntityTypeConfiguration<ChatMemberSettingRecord>
{
    public void Configure(EntityTypeBuilder<ChatMemberSettingRecord> builder)
    {
        builder.ToTable("chat_member_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.ChatRoomId).HasColumnName("chat_room_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.NoteName).HasColumnName("note_name").HasMaxLength(100);
        builder.Property(x => x.Pinned).HasColumnName("pinned").HasDefaultValue(false);
        builder.Property(x => x.Muted).HasColumnName("muted").HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(x => new { x.UserId, x.ChatRoomId }).IsUnique();
    }
}

public sealed class UserBlockRecordConfiguration : IEntityTypeConfiguration<UserBlockRecord>
{
    public void Configure(EntityTypeBuilder<UserBlockRecord> builder)
    {
        builder.ToTable("user_blocks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.BlockerId).HasColumnName("blocker_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.BlockedId).HasColumnName("blocked_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.BlockerId, x.BlockedId }).IsUnique();
    }
}

public sealed class ChatReportRecordConfiguration : IEntityTypeConfiguration<ChatReportRecord>
{
    public void Configure(EntityTypeBuilder<ChatReportRecord> builder)
    {
        builder.ToTable("chat_reports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.ReporterId).HasColumnName("reporter_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ReportedUserId).HasColumnName("reported_user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ChatRoomId).HasColumnName("chat_room_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}
