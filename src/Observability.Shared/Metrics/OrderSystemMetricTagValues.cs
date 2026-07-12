namespace Observability.Shared.Metrics;

public static class OrderSystemMetricTagValues
{
    public const string Unknown = "unknown";

    public const string Success = "success";
    public const string Failure = "failure";

    public const string RabbitMq = "rabbitmq";

    public const string Publish = "publish";
    public const string Consume = "consume";
    public const string DeadLetter = "dead_letter";

    public const string Reserved = "reserved";
    public const string Failed = "failed";
}