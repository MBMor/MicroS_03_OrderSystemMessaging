namespace NotificationsService.Application.EventNotifications.Contracts;

public sealed record CreateStockReservationFailedNotificationCommand
{
    public Guid MessageId { get; init; }

    public string? EventType { get; init; }

    public string? CorrelationId { get; init; }

    public Guid OrderId { get; init; }

    public string? CustomerName { get; init; }

    public string? CustomerEmail { get; init; }

    public string? Reason { get; init; }
}