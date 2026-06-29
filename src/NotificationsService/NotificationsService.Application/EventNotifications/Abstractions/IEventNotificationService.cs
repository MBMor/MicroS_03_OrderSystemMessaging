using NotificationsService.Application.EventNotifications.Contracts;

namespace NotificationsService.Application.EventNotifications.Abstractions;

public interface IEventNotificationService
{
    Task HandleOrderCreatedAsync(
        CreateOrderCreatedNotificationCommand command,
        CancellationToken cancellationToken);

    Task HandleStockReservedAsync(
        CreateStockReservedNotificationCommand command,
        CancellationToken cancellationToken);

    Task HandleStockReservationFailedAsync(
        CreateStockReservationFailedNotificationCommand command,
        CancellationToken cancellationToken);
}