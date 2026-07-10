using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Observability.Shared.Metrics;

public static class OrderSystemOutboxMetrics
{
    private static readonly Counter<long> OutboxMessagesPublishedTotal =
        OrderSystemMeters.Outbox.CreateCounter<long>(
            name: OrderSystemMetricNames.OutboxMessagesPublishedTotal,
            unit: "{message}",
            description: "Number of outbox messages published successfully.");

    private static readonly Counter<long> OutboxMessagesFailedTotal =
        OrderSystemMeters.Outbox.CreateCounter<long>(
            name: OrderSystemMetricNames.OutboxMessagesFailedTotal,
            unit: "{message}",
            description: "Number of outbox messages permanently failed.");

    private static readonly Counter<long> OutboxMessagesRetriedTotal =
        OrderSystemMeters.Outbox.CreateCounter<long>(
            name: OrderSystemMetricNames.OutboxMessagesRetriedTotal,
            unit: "{message}",
            description: "Number of outbox messages scheduled for retry.");

    public static void RecordPublished(
        string? eventType,
        string? routingKey,
        string? outboxStatus)
    {
        var tags = CreateOutboxTags(
            eventType,
            routingKey,
            outboxStatus);

        OutboxMessagesPublishedTotal.Add(1, tags);
    }

    public static void RecordFailed(
        string? eventType,
        string? routingKey,
        string? outboxStatus,
        Exception exception)
    {
        var tags = CreateOutboxTags(
            eventType,
            routingKey,
            outboxStatus);

        tags.Add(
            OrderSystemMetricTagNames.ErrorType,
            OrderSystemMetricTagHelper.Normalize(exception));

        OutboxMessagesFailedTotal.Add(1, tags);
    }

    public static void RecordRetried(
        string? eventType,
        string? routingKey,
        string? outboxStatus,
        Exception exception)
    {
        var tags = CreateOutboxTags(
            eventType,
            routingKey,
            outboxStatus);

        tags.Add(
            OrderSystemMetricTagNames.ErrorType,
            OrderSystemMetricTagHelper.Normalize(exception));

        OutboxMessagesRetriedTotal.Add(1, tags);
    }

    private static TagList CreateOutboxTags(
        string? eventType,
        string? routingKey,
        string? outboxStatus)
    {
        var tags = new TagList();

        tags.Add(
            OrderSystemMetricTagNames.EventType,
            OrderSystemMetricTagHelper.Normalize(eventType));

        tags.Add(
            OrderSystemMetricTagNames.MessagingRabbitMqRoutingKey,
            OrderSystemMetricTagHelper.Normalize(routingKey));

        tags.Add(
            OrderSystemMetricTagNames.OutboxStatus,
            OrderSystemMetricTagHelper.Normalize(outboxStatus));

        return tags;
    }
}