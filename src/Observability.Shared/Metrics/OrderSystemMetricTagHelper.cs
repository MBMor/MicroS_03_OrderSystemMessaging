namespace Observability.Shared.Metrics;

internal static class OrderSystemMetricTagHelper
{
    public const string Unknown = "unknown";

    public static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Unknown
            : value.Trim();
    }

    public static string Normalize(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.GetType().FullName
            ?? exception.GetType().Name;
    }
}