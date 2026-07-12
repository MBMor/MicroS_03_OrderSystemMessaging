namespace Observability.Shared.Metrics;

public static class OrderSystemMetricNames
{
    public const string OrdersCreatedTotal = "orders.created.total";
    public const string OrdersStockReservedTotal = "orders.stock_reserved.total";
    public const string OrdersStockReservationFailedTotal = "orders.stock_reservation_failed.total";

    public const string InventoryStockReservationsTotal = "inventory.stock_reservations.total";
    public const string InventoryStockReservationFailuresTotal = "inventory.stock_reservation_failures.total";

    public const string NotificationsCreatedTotal = "notifications.created.total";

    public const string OutboxMessagesPublishedTotal = "outbox.messages.published.total";
    public const string OutboxMessagesFailedTotal = "outbox.messages.failed.total";
    public const string OutboxMessagesRetriedTotal = "outbox.messages.retried.total";
    public const string OutboxPublishDurationMilliseconds = "outbox.publish.duration.ms";

    public const string RabbitMqMessagesPublishedTotal = "rabbitmq.messages.published.total";
    public const string RabbitMqMessagesConsumedTotal = "rabbitmq.messages.consumed.total";
    public const string RabbitMqMessagesFailedTotal = "rabbitmq.messages.failed.total";
    public const string RabbitMqMessagesDeadLetteredTotal = "rabbitmq.messages.dead_lettered.total";
    public const string RabbitMqConsumeDurationMilliseconds = "rabbitmq.consume.duration.ms";
}