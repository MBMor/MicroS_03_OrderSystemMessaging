using System.Diagnostics.Metrics;

namespace Observability.Shared.Metrics;

public static class OrderSystemMeters
{
    public static readonly Meter Orders = new(OrderSystemMeterNames.OrdersName);
    public static readonly Meter Inventory = new(OrderSystemMeterNames.InventoryName);
    public static readonly Meter Notifications = new(OrderSystemMeterNames.NotificationsName);
    public static readonly Meter Outbox = new(OrderSystemMeterNames.OutboxName);
    public static readonly Meter Messaging = new(OrderSystemMeterNames.MessagingName);
}