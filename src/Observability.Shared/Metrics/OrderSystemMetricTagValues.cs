namespace Observability.Shared.Metrics;

public static class OrderSystemMetricTagValues
{
    public const string RabbitMq = "rabbitmq";

    public const string Publish = "publish";
    public const string Consume = "consume";

    public const string Reserved = "reserved";
    public const string Failed = "failed";
}