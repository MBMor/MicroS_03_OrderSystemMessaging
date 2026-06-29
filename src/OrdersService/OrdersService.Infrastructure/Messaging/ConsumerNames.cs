namespace OrdersService.Infrastructure.Messaging;

public static class ConsumerNames
{
    public const string OrdersStockReservedConsumer = "orders.stock-reserved-consumer";

    public const string OrdersStockReservationFailedConsumer = "orders.stock-reservation-failed-consumer";
}