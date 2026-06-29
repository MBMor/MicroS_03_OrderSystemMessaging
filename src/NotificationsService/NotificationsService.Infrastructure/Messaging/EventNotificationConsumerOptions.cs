namespace NotificationsService.Infrastructure.Messaging;

public sealed class EventNotificationConsumerOptions
{
    public const string SectionName = "EventNotificationConsumers";

    public ushort PrefetchCount { get; init; } = 10;

    public int ConnectionRetryDelaySeconds { get; init; } = 5;
}