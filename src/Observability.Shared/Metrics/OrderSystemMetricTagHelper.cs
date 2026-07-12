namespace Observability.Shared.Metrics;

internal static class OrderSystemMetricTagHelper
{
    public static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? OrderSystemMetricTagValues.Unknown
            : value.Trim();
    }

    public static string Normalize(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.GetType().FullName
            ?? exception.GetType().Name;
    }
}