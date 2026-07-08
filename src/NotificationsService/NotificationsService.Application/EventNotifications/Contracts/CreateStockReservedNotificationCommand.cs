namespace NotificationsService.Application.EventNotifications.Contracts;

public sealed record CreateStockReservedNotificationCommand
{
    public Guid MessageId { get; init; }

    public string? EventType { get; init; }

    public string? CorrelationId { get; init; }

    public Guid OrderId { get; init; }

    public string? CustomerName { get; init; }

    public string? CustomerEmail { get; init; }
}