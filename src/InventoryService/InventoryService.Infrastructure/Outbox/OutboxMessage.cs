namespace InventoryService.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public const int EventTypeMaxLength = 200;
    public const int RoutingKeyMaxLength = 200;
    public const int LastErrorMaxLength = 4000;

    private OutboxMessage()
    {
        EventType = string.Empty;
        RoutingKey = string.Empty;
        Payload = string.Empty;
    }

    public OutboxMessage(
        Guid id,
        Guid eventId,
        string eventType,
        string routingKey,
        string payload,
        DateTime occurredAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Outbox message ID is required.", nameof(id));
        }

        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event ID is required.", nameof(eventId));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Event type is required.", nameof(eventType));
        }

        if (eventType.Length > EventTypeMaxLength)
        {
            throw new ArgumentException($"Event type must not exceed {EventTypeMaxLength} characters.", nameof(eventType));
        }

        if (string.IsNullOrWhiteSpace(routingKey))
        {
            throw new ArgumentException("Routing key is required.", nameof(routingKey));
        }

        if (routingKey.Length > RoutingKeyMaxLength)
        {
            throw new ArgumentException($"Routing key must not exceed {RoutingKeyMaxLength} characters.", nameof(routingKey));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload is required.", nameof(payload));
        }

        Id = id;
        EventId = eventId;
        EventType = eventType.Trim();
        RoutingKey = routingKey.Trim();
        Payload = payload;
        OccurredAtUtc = EnsureUtc(occurredAtUtc);
        Status = OutboxStatus.Pending;
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string EventType { get; private set; }

    public string RoutingKey { get; private set; }

    public string Payload { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public int RetryCount { get; private set; }

    public string? LastError { get; private set; }

    public OutboxStatus Status { get; private set; }

    public void MarkPublished(DateTime processedAtUtc)
    {
        Status = OutboxStatus.Published;
        ProcessedAtUtc = EnsureUtc(processedAtUtc);
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Status = OutboxStatus.Failed;
        LastError = NormalizeError(error);
        RetryCount++;
    }

    public void MarkPendingForRetry(string error)
    {
        Status = OutboxStatus.Pending;
        LastError = NormalizeError(error);
        RetryCount++;
    }

    private static string NormalizeError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "Unknown outbox publishing error.";
        }

        var normalized = error.Trim();

        return normalized.Length <= LastErrorMaxLength
            ? normalized
            : normalized[..LastErrorMaxLength];
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