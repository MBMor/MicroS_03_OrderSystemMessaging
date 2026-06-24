using InventoryService.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryService.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(outboxMessage => outboxMessage.Id);

        builder.Property(outboxMessage => outboxMessage.EventId)
            .IsRequired();

        builder.Property(outboxMessage => outboxMessage.EventType)
            .HasMaxLength(OutboxMessage.EventTypeMaxLength)
            .IsRequired();

        builder.Property(outboxMessage => outboxMessage.RoutingKey)
            .HasMaxLength(OutboxMessage.RoutingKeyMaxLength)
            .IsRequired();

        builder.Property(outboxMessage => outboxMessage.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(outboxMessage => outboxMessage.OccurredAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(outboxMessage => outboxMessage.ProcessedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(outboxMessage => outboxMessage.RetryCount)
            .IsRequired();

        builder.Property(outboxMessage => outboxMessage.LastError)
            .HasMaxLength(OutboxMessage.LastErrorMaxLength);

        builder.Property(outboxMessage => outboxMessage.Status)
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(outboxMessage => outboxMessage.EventId);

        builder.HasIndex(outboxMessage => outboxMessage.Status);

        builder.HasIndex(outboxMessage => new
        {
            outboxMessage.Status,
            outboxMessage.OccurredAtUtc
        });
    }
}