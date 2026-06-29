namespace OrdersService.Infrastructure.Messaging;

public static class RabbitMqRoutingKeys
{
    public const string OrderCreated = "order.created";

    public const string StockReserved = "stock.reserved";

    public const string StockReservationFailed = "stock.reservation.failed";

    public const string OrdersStockReservedDeadLetter = "orders.stock-reserved.dead-letter";

    public const string OrdersStockReservationFailedDeadLetter = "orders.stock-reservation-failed.dead-letter";
}