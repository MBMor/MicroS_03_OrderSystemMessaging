namespace Observability.Shared.Metrics;

public static class OrderSystemMetricTagNames
{
    public const string ServiceName = "service.name";

    public const string OrderStatus = "order.status";

    public const string ReservationStatus = "stock.reservation.status";
    public const string ReservationFailureReason = "stock.reservation.failure_reason";

    public const string NotificationType = "notification.type";

    public const string EventType = "event.type";

    public const string OutboxStatus = "outbox.status";

    public const string MessagingSystem = "messaging.system";
    public const string MessagingOperation = "messaging.operation.name";
    public const string MessagingDestinationName = "messaging.destination.name";
    public const string MessagingRabbitMqRoutingKey = "messaging.rabbitmq.routing_key";
    public const string MessagingRabbitMqQueueName = "messaging.rabbitmq.queue.name";

    public const string ErrorType = "error.type";
}