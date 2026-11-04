using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Infrastructure.Persistence.Configurations;

public sealed class AiUsageLogRecordConfiguration : IEntityTypeConfiguration<AiUsageLogRecord>
{
    public void Configure(EntityTypeBuilder<AiUsageLogRecord> builder)
    {
        builder.ToTable("ai_usage_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(x => x.TodayEntryId).HasColumnName("today_entry_id").HasMaxLength(64);
        builder.Property(x => x.Model).HasColumnName("model").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.InputTokens).HasColumnName("input_tokens");
        builder.Property(x => x.CachedInputTokens).HasColumnName("cached_input_tokens");
        builder.Property(x => x.OutputTokens).HasColumnName("output_tokens");
        builder.Property(x => x.ReasoningTokens).HasColumnName("reasoning_tokens");

        // 金額要明寫精度。單筆成本在 $0.00005 這個量級，用浮點數彙總起來會對不上；
        // decimal 不給精度的話 Npgsql 會警告，而且預設精度不夠放小數第八位。
        builder.Property(x => x.EstimatedCostUsd)
            .HasColumnName("estimated_cost_usd").HasColumnType("numeric(14, 8)");
        builder.Property(x => x.InputPricePerMTok)
            .HasColumnName("input_price_per_mtok").HasColumnType("numeric(10, 4)");
        builder.Property(x => x.CachedPricePerMTok)
            .HasColumnName("cached_price_per_mtok").HasColumnType("numeric(10, 4)");
        builder.Property(x => x.OutputPricePerMTok)
            .HasColumnName("output_price_per_mtok").HasColumnType("numeric(10, 4)");

        builder.Property(x => x.LatencyMs).HasColumnName("latency_ms");
        builder.Property(x => x.Error).HasColumnName("error").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.Model, x.CreatedAt });
    }
}
