namespace NotificationsService.Infrastructure.Messaging;

public static class RabbitMqRoutingKeys
{
    public const string OrderCreated = "order.created";

    public const string StockReserved = "stock.reserved";

    public const string StockReservationFailed = "stock.reservation.failed";

    public const string NotificationsOrderCreatedDeadLetter = "notifications.order-created.dead-letter";

    public const string NotificationsStockReservedDeadLetter = "notifications.stock-reserved.dead-letter";

    public const string NotificationsStockReservationFailedDeadLetter = "notifications.stock-reservation-failed.dead-letter";
}