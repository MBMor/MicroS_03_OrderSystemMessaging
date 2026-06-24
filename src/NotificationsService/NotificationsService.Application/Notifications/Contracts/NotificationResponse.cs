namespace NotificationsService.Application.Notifications.Contracts;

public sealed record NotificationResponse
{
    public required Guid Id { get; init; }

    public required Guid SourceEventId { get; init; }

    public required string SourceEventType { get; init; }

    public required string Recipient { get; init; }

    public required string Subject { get; init; }

    public required string Body { get; init; }

    public required string Status { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}