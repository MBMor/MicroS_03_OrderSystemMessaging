namespace InventoryService.Infrastructure.Outbox;

public sealed class OutboxPublisherOptions
{
    public const string SectionName = "OutboxPublisher";

    public int BatchSize { get; init; } = 20;

    public int PollingIntervalSeconds { get; init; } = 5;

    public int MaxRetryCount { get; init; } = 100;
}