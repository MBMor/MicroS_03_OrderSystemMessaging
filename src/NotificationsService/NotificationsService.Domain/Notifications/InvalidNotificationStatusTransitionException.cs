using NotificationsService.Domain.Common;

namespace NotificationsService.Domain.Notifications;

public sealed class InvalidNotificationStatusTransitionException(
    NotificationStatus currentStatus,
    NotificationStatus requestedStatus) 
    : DomainException($"Cannot change notification status from '{currentStatus}' to '{requestedStatus}'.")
{
    public NotificationStatus CurrentStatus { get; } = currentStatus;

    public NotificationStatus RequestedStatus { get; } = requestedStatus;
}