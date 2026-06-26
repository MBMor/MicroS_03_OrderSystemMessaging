namespace InventoryService.Infrastructure.Messaging;

public sealed class OrderCreatedConsumerOptions
{
    public const string SectionName = "OrderCreatedConsumer";

    public ushort PrefetchCount { get; init; } = 10;

    public int ConnectionRetryDelaySeconds { get; init; } = 5;
}