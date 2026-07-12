namespace Observability.Shared.Logging;

public static class OrderSystemLogPropertyNames
{
    public const string ErrorType = "ErrorType";

    public const string CorrelationId = "CorrelationId";

    public const string EventId = "EventId";
    public const string EventType = "EventType";

    public const string OutboxMessageId = "OutboxMessageId";
    public const string OutboxStatus = "OutboxStatus";
    public const string RetryCount = "RetryCount";
    public const string MaxRetryCount = "MaxRetryCount";

    public const string QueueName = "QueueName";
    public const string ExchangeName = "ExchangeName";
    public const string RoutingKey = "RoutingKey";
    public const string DeliveryTag = "DeliveryTag";
    public const string Redelivered = "Redelivered";
}