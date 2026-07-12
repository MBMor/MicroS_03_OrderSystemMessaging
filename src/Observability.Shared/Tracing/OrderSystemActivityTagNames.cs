namespace Observability.Shared.Tracing;

public static class OrderSystemActivityTagNames
{
    public const string CorrelationId = "correlation.id";

    public const string OrderId = "order.id";
    public const string OrderStatus = "order.status";
    public const string OrderItemCount = "order.item_count";

    public const string ProductId = "product.id";
    public const string ReservationId = "stock.reservation.id";
    public const string ReservationStatus = "stock.reservation.status";
    public const string ReservationFailureReason = "stock.reservation.failure_reason";

    public const string NotificationId = "notification.id";
    public const string NotificationType = "notification.type";

    public const string EventId = "event.id";
    public const string EventType = "event.type";

    public const string OutboxMessageId = "outbox.message.id";
    public const string OutboxMessageStatus = "outbox.message.status";
    public const string OutboxBatchSize = "outbox.batch_size";
    public const string OutboxRetryCount = "outbox.retry_count";

    public const string MessagingSystem = "messaging.system";
    public const string MessagingOperation = "messaging.operation.name";
    public const string MessagingDestinationName = "messaging.destination.name";
    public const string MessagingMessageId = "messaging.message.id";
    public const string MessagingRabbitMqRoutingKey = "messaging.rabbitmq.routing_key";
    public const string MessagingRabbitMqQueueName = "messaging.rabbitmq.queue.name";
    public const string MessagingRabbitMqDeadLetterQueueName = "messaging.rabbitmq.dead_letter_queue.name";
    public const string MessagingRabbitMqDeliveryTag = "messaging.rabbitmq.delivery_tag";
    public const string MessagingRabbitMqRedelivered = "messaging.rabbitmq.redelivered";

    public const string ErrorType = "error.type";
}