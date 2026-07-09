using System.Diagnostics;

namespace Observability.Shared.Tracing;

public static class OrderSystemActivitySources
{
    public const string OrdersName = "OrderSystem.Orders";
    public const string InventoryName = "OrderSystem.Inventory";
    public const string NotificationsName = "OrderSystem.Notifications";
    public const string OutboxName = "OrderSystem.Outbox";
    public const string MessagingName = "OrderSystem.Messaging";

    public static readonly ActivitySource Orders = new(OrdersName);
    public static readonly ActivitySource Inventory = new(InventoryName);
    public static readonly ActivitySource Notifications = new(NotificationsName);
    public static readonly ActivitySource Outbox = new(OutboxName);
    public static readonly ActivitySource Messaging = new(MessagingName);
}