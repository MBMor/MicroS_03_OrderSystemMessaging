using NotificationsService.Application.Common.Pagination;
using NotificationsService.Application.Notifications.Contracts;

namespace NotificationsService.Application.Notifications.Abstractions;

public interface INotificationsService
{
    Task<NotificationResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<PagedResult<NotificationResponse>> ListAsync(
        ListNotificationsRequest request,
        CancellationToken cancellationToken);
}