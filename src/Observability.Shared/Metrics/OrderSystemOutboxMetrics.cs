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

    private static readonly Histogram<double> OutboxPublishDurationMilliseconds =
        OrderSystemMeters.Outbox.CreateHistogram<double>(
            name: OrderSystemMetricNames.OutboxPublishDurationMilliseconds,
            unit: "ms",
            description: "Duration of publishing one outbox message.");

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

    public static void RecordPublishDuration(
        TimeSpan duration,
        string? eventType,
        string? routingKey,
        string? outboxStatus,
        string outcome)
    {
        var tags = CreateOutboxTags(
            eventType,
            routingKey,
            outboxStatus);

        tags.Add(
            OrderSystemMetricTagNames.Outcome,
            OrderSystemMetricTagHelper.Normalize(outcome));

        OutboxPublishDurationMilliseconds.Record(
            duration.TotalMilliseconds,
            tags);
    }

    private static TagList CreateOutboxTags(
        string? eventType,
        string? routingKey,
        string? outboxStatus)
    {
        return new TagList
        {
            {
                OrderSystemMetricTagNames.EventType,
                OrderSystemMetricTagHelper.Normalize(eventType)
            },
            {
                OrderSystemMetricTagNames.MessagingRabbitMqRoutingKey,
                OrderSystemMetricTagHelper.Normalize(routingKey)
            },
            {
                OrderSystemMetricTagNames.OutboxStatus,
                OrderSystemMetricTagHelper.Normalize(outboxStatus)
            }
        };
    }
}