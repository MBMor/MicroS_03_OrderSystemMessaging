using NotificationsService.Domain.Common;

namespace NotificationsService.Domain.Notifications;

public sealed class Notification
{
    public const int SourceEventTypeMaxLength = 200;
    public const int RecipientMaxLength = 320;
    public const int SubjectMaxLength = 300;
    public const int BodyMaxLength = 4000;

    private Notification()
    {
        SourceEventType = string.Empty;
        Recipient = string.Empty;
        Subject = string.Empty;
        Body = string.Empty;
    }

    public Notification(
        Guid id,
        Guid sourceEventId,
        string sourceEventType,
        string recipient,
        string subject,
        string body,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Notification ID is required.");
        }

        if (sourceEventId == Guid.Empty)
        {
            throw new DomainException("Source event ID is required.");
        }

        Id = id;
        SourceEventId = sourceEventId;
        SourceEventType = ValidateRequiredText(
            sourceEventType,
            nameof(SourceEventType),
            SourceEventTypeMaxLength);

        Recipient = ValidateRequiredText(
            recipient,
            nameof(Recipient),
            RecipientMaxLength);

        Subject = ValidateRequiredText(
            subject,
            nameof(Subject),
            SubjectMaxLength);

        Body = ValidateRequiredText(
            body,
            nameof(Body),
            BodyMaxLength);

        Status = NotificationStatus.Created;
        CreatedAtUtc = EnsureUtc(createdAtUtc);
    }

    public Guid Id { get; private set; }

    public Guid SourceEventId { get; private set; }

    public string SourceEventType { get; private set; }

    public string Recipient { get; private set; }

    public string Subject { get; private set; }

    public string Body { get; private set; }

    public NotificationStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public void MarkProcessed()
    {
        ChangeStatus(NotificationStatus.Processed);
    }

    public void MarkFailed()
    {
        ChangeStatus(NotificationStatus.Failed);
    }

    private void ChangeStatus(NotificationStatus requestedStatus)
    {
        if (!CanChangeStatusTo(requestedStatus))
        {
            throw new InvalidNotificationStatusTransitionException(Status, requestedStatus);
        }

        Status = requestedStatus;
    }

    private bool CanChangeStatusTo(NotificationStatus requestedStatus)
    {
        return Status switch
        {
            NotificationStatus.Created => requestedStatus is
                NotificationStatus.Processed or
                NotificationStatus.Failed,

            _ => false
        };
    }

    private static string ValidateRequiredText(
        string value,
        string propertyName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{propertyName} is required.");
        }

        if (value.Length > maxLength)
        {
            throw new DomainException($"{propertyName} must not exceed {maxLength} characters.");
        }

        return value.Trim();
    }

    private static DateTime EnsureUtc(DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
            _ => dateTime
        };
    }
}