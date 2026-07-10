using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Observability.Shared.Metrics;

public static class OrderSystemMessagingMetrics
{
    private static readonly Counter<long> RabbitMqMessagesPublishedTotal =
        OrderSystemMeters.Messaging.CreateCounter<long>(
            name: OrderSystemMetricNames.RabbitMqMessagesPublishedTotal,
            unit: "{message}",
            description: "Number of RabbitMQ messages published.");

    private static readonly Counter<long> RabbitMqMessagesConsumedTotal =
        OrderSystemMeters.Messaging.CreateCounter<long>(
            name: OrderSystemMetricNames.RabbitMqMessagesConsumedTotal,
            unit: "{message}",
            description: "Number of RabbitMQ messages consumed successfully.");

    private static readonly Counter<long> RabbitMqMessagesFailedTotal =
        OrderSystemMeters.Messaging.CreateCounter<long>(
            name: OrderSystemMetricNames.RabbitMqMessagesFailedTotal,
            unit: "{message}",
            description: "Number of RabbitMQ messages that failed during processing.");

    public static void RecordPublished(
        string? exchangeName,
        string? routingKey,
        string? eventType)
    {
        var tags = new TagList();

        tags.Add(
            OrderSystemMetricTagNames.MessagingSystem,
            OrderSystemMetricTagValues.RabbitMq);

        tags.Add(
            OrderSystemMetricTagNames.MessagingOperation,
            OrderSystemMetricTagValues.Publish);

        tags.Add(
            OrderSystemMetricTagNames.MessagingDestinationName,
            OrderSystemMetricTagHelper.Normalize(exchangeName));

        tags.Add(
            OrderSystemMetricTagNames.MessagingRabbitMqRoutingKey,
            OrderSystemMetricTagHelper.Normalize(routingKey));

        tags.Add(
            OrderSystemMetricTagNames.EventType,
            OrderSystemMetricTagHelper.Normalize(eventType));

        RabbitMqMessagesPublishedTotal.Add(1, tags);
    }

    public static void RecordConsumed(
        string? queueName,
        string? routingKey,
        string? eventType)
    {
        var tags = new TagList();

        tags.Add(
            OrderSystemMetricTagNames.MessagingSystem,
            OrderSystemMetricTagValues.RabbitMq);

        tags.Add(
            OrderSystemMetricTagNames.MessagingOperation,
            OrderSystemMetricTagValues.Consume);

        tags.Add(
            OrderSystemMetricTagNames.MessagingRabbitMqQueueName,
            OrderSystemMetricTagHelper.Normalize(queueName));

        tags.Add(
            OrderSystemMetricTagNames.MessagingRabbitMqRoutingKey,
            OrderSystemMetricTagHelper.Normalize(routingKey));

        tags.Add(
            OrderSystemMetricTagNames.EventType,
            OrderSystemMetricTagHelper.Normalize(eventType));

        RabbitMqMessagesConsumedTotal.Add(1, tags);
    }

    public static void RecordFailed(
        string? queueName,
        string? routingKey,
        string? eventType,
        Exception exception)
    {
        var tags = new TagList();

        tags.Add(
            OrderSystemMetricTagNames.MessagingSystem,
            OrderSystemMetricTagValues.RabbitMq);

        tags.Add(
            OrderSystemMetricTagNames.MessagingOperation,
            OrderSystemMetricTagValues.Consume);

        tags.Add(
            OrderSystemMetricTagNames.MessagingRabbitMqQueueName,
            OrderSystemMetricTagHelper.Normalize(queueName));

        tags.Add(
            OrderSystemMetricTagNames.MessagingRabbitMqRoutingKey,
            OrderSystemMetricTagHelper.Normalize(routingKey));

        tags.Add(
            OrderSystemMetricTagNames.EventType,
            OrderSystemMetricTagHelper.Normalize(eventType));

        tags.Add(
            OrderSystemMetricTagNames.ErrorType,
            OrderSystemMetricTagHelper.Normalize(exception));

        RabbitMqMessagesFailedTotal.Add(1, tags);
    }
}