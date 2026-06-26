namespace InventoryService.Infrastructure.Messaging;

public sealed class RabbitMqTopologyOptions
{
    public const string SectionName = "RabbitMQTopology";

    public string DeadLetterExchangeName { get; init; } = "ordersystem.events.dlx";

    public string OrderCreatedQueueName { get; init; } = "inventory.order-created";

    public string OrderCreatedDeadLetterQueueName { get; init; } = "inventory.order-created.dlq";

    public int InitializationRetryDelaySeconds { get; init; } = 5;
}