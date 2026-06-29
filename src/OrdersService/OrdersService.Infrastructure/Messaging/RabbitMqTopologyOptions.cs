namespace OrdersService.Infrastructure.Messaging;

public sealed class RabbitMqTopologyOptions
{
    public const string SectionName = "RabbitMQTopology";

    public string DeadLetterExchangeName { get; init; } = "ordersystem.events.dlx";

    public string StockReservedQueueName { get; init; } = "orders.stock-reserved";

    public string StockReservedDeadLetterQueueName { get; init; } = "orders.stock-reserved.dlq";

    public string StockReservationFailedQueueName { get; init; } = "orders.stock-reservation-failed";

    public string StockReservationFailedDeadLetterQueueName { get; init; } = "orders.stock-reservation-failed.dlq";

    public int InitializationRetryDelaySeconds { get; init; } = 5;
}