namespace NotificationsService.Infrastructure.Messaging;

public sealed class RabbitMqTopologyOptions
{
    public const string SectionName = "RabbitMQTopology";

    public string DeadLetterExchangeName { get; init; } = "ordersystem.events.dlx";

    public string OrderCreatedQueueName { get; init; } = "notifications.order-created";

    public string OrderCreatedDeadLetterQueueName { get; init; } = "notifications.order-created.dlq";

    public string StockReservedQueueName { get; init; } = "notifications.stock-reserved";

    public string StockReservedDeadLetterQueueName { get; init; } = "notifications.stock-reserved.dlq";

    public string StockReservationFailedQueueName { get; init; } = "notifications.stock-reservation-failed";

    public string StockReservationFailedDeadLetterQueueName { get; init; } = "notifications.stock-reservation-failed.dlq";

    public int InitializationRetryDelaySeconds { get; init; } = 5;
}