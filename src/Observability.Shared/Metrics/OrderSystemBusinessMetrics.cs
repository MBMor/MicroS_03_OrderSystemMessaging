using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Observability.Shared.Metrics;

public static class OrderSystemBusinessMetrics
{
    private static readonly Counter<long> OrdersCreatedTotal =
        OrderSystemMeters.Orders.CreateCounter<long>(
            name: OrderSystemMetricNames.OrdersCreatedTotal,
            unit: "{order}",
            description: "Number of orders created.");

    private static readonly Counter<long> OrdersStockReservedTotal =
        OrderSystemMeters.Orders.CreateCounter<long>(
            name: OrderSystemMetricNames.OrdersStockReservedTotal,
            unit: "{order}",
            description: "Number of orders marked as stock reserved.");

    private static readonly Counter<long> OrdersStockReservationFailedTotal =
        OrderSystemMeters.Orders.CreateCounter<long>(
            name: OrderSystemMetricNames.OrdersStockReservationFailedTotal,
            unit: "{order}",
            description: "Number of orders marked as stock reservation failed.");

    private static readonly Counter<long> InventoryStockReservationsTotal =
        OrderSystemMeters.Inventory.CreateCounter<long>(
            name: OrderSystemMetricNames.InventoryStockReservationsTotal,
            unit: "{reservation}",
            description: "Number of successful inventory stock reservations.");

    private static readonly Counter<long> InventoryStockReservationFailuresTotal =
        OrderSystemMeters.Inventory.CreateCounter<long>(
            name: OrderSystemMetricNames.InventoryStockReservationFailuresTotal,
            unit: "{reservation}",
            description: "Number of failed inventory stock reservations.");

    private static readonly Counter<long> NotificationsCreatedTotal =
        OrderSystemMeters.Notifications.CreateCounter<long>(
            name: OrderSystemMetricNames.NotificationsCreatedTotal,
            unit: "{notification}",
            description: "Number of notifications created.");

    public static void RecordOrderCreated(string? orderStatus)
    {
        var tags = new TagList();
        tags.Add(
            OrderSystemMetricTagNames.OrderStatus,
            OrderSystemMetricTagHelper.Normalize(orderStatus));

        OrdersCreatedTotal.Add(1, tags);
    }

    public static void RecordOrderStockReserved(string? orderStatus)
    {
        var tags = new TagList();
        tags.Add(
            OrderSystemMetricTagNames.OrderStatus,
            OrderSystemMetricTagHelper.Normalize(orderStatus));

        OrdersStockReservedTotal.Add(1, tags);
    }

    public static void RecordOrderStockReservationFailed(
        string? orderStatus,
        string? failureReason)
    {
        var tags = new TagList();
        tags.Add(
            OrderSystemMetricTagNames.OrderStatus,
            OrderSystemMetricTagHelper.Normalize(orderStatus));
        tags.Add(
            OrderSystemMetricTagNames.ReservationFailureReason,
            OrderSystemMetricTagHelper.Normalize(failureReason));

        OrdersStockReservationFailedTotal.Add(1, tags);
    }

    public static void RecordInventoryStockReserved()
    {
        var tags = new TagList();
        tags.Add(
            OrderSystemMetricTagNames.ReservationStatus,
            OrderSystemMetricTagValues.Reserved);

        InventoryStockReservationsTotal.Add(1, tags);
    }

    public static void RecordInventoryStockReservationFailed(string? failureReason)
    {
        var tags = new TagList();
        tags.Add(
            OrderSystemMetricTagNames.ReservationStatus,
            OrderSystemMetricTagValues.Failed);
        tags.Add(
            OrderSystemMetricTagNames.ReservationFailureReason,
            OrderSystemMetricTagHelper.Normalize(failureReason));

        InventoryStockReservationFailuresTotal.Add(1, tags);
    }

    public static void RecordNotificationCreated(string? notificationType)
    {
        var tags = new TagList();
        tags.Add(
            OrderSystemMetricTagNames.NotificationType,
            OrderSystemMetricTagHelper.Normalize(notificationType));

        NotificationsCreatedTotal.Add(1, tags);
    }
}