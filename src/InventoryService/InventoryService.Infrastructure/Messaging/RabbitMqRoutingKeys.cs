namespace InventoryService.Infrastructure.Messaging;

public static class RabbitMqRoutingKeys
{
    public const string OrderCreated = "order.created";

    public const string InventoryOrderCreatedDeadLetter = "inventory.order-created.dead-letter";
}