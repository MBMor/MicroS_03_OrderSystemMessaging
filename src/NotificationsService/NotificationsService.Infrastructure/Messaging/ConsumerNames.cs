namespace NotificationsService.Infrastructure.Messaging;

public static class ConsumerNames
{
    public const string NotificationsOrderCreatedConsumer = "notifications.order-created-consumer";

    public const string NotificationsStockReservedConsumer = "notifications.stock-reserved-consumer";

    public const string NotificationsStockReservationFailedConsumer = "notifications.stock-reservation-failed-consumer";
}