namespace InventoryService.Infrastructure.Idempotency;

public sealed class ProcessedMessage
{
    public const int EventTypeMaxLength = 200;
    public const int ConsumerNameMaxLength = 200;

    private ProcessedMessage()
    {
        EventType = string.Empty;
        ConsumerName = string.Empty;
    }

    public ProcessedMessage(
        Guid id,
        Guid messageId,
        string eventType,
        string consumerName,
        DateTime processedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Processed message ID is required.", nameof(id));
        }

        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("Message ID is required.", nameof(messageId));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Event type is required.", nameof(eventType));
        }

        if (eventType.Length > EventTypeMaxLength)
        {
            throw new ArgumentException($"Event type must not exceed {EventTypeMaxLength} characters.", nameof(eventType));
        }

        if (string.IsNullOrWhiteSpace(consumerName))
        {
            throw new ArgumentException("Consumer name is required.", nameof(consumerName));
        }

        if (consumerName.Length > ConsumerNameMaxLength)
        {
            throw new ArgumentException($"Consumer name must not exceed {ConsumerNameMaxLength} characters.", nameof(consumerName));
        }

        Id = id;
        MessageId = messageId;
        EventType = eventType.Trim();
        ConsumerName = consumerName.Trim();
        ProcessedAtUtc = EnsureUtc(processedAtUtc);
    }

    public Guid Id { get; private set; }

    public Guid MessageId { get; private set; }

    public string EventType { get; private set; }

    public string ConsumerName { get; private set; }

    public DateTime ProcessedAtUtc { get; private set; }

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