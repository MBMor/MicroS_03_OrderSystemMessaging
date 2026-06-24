using InventoryService.Infrastructure.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryService.Infrastructure.Persistence.Configurations;

public sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("ProcessedMessages");

        builder.HasKey(processedMessage => processedMessage.Id);

        builder.Property(processedMessage => processedMessage.MessageId)
            .IsRequired();

        builder.Property(processedMessage => processedMessage.EventType)
            .HasMaxLength(ProcessedMessage.EventTypeMaxLength)
            .IsRequired();

        builder.Property(processedMessage => processedMessage.ConsumerName)
            .HasMaxLength(ProcessedMessage.ConsumerNameMaxLength)
            .IsRequired();

        builder.Property(processedMessage => processedMessage.ProcessedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(processedMessage => new
        {
            processedMessage.MessageId,
            processedMessage.EventType,
            processedMessage.ConsumerName
        }).IsUnique();

        builder.HasIndex(processedMessage => processedMessage.ProcessedAtUtc);
    }
}