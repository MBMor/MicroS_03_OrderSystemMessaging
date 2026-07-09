using System.Diagnostics;

namespace Observability.Shared.Tracing;

public static class ActivityExtensions
{
    public static void SetTagIfNotNull(
        this Activity? activity,
        string key,
        object? value)
    {
        if (activity is null || value is null)
        {
            return;
        }

        activity.SetTag(key, value);
    }

    public static void SetError(
        this Activity? activity,
        Exception exception)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(
            ActivityStatusCode.Error,
            exception.Message);

        activity.SetTag(
            OrderSystemActivityTagNames.ErrorType,
            exception.GetType().FullName);

        activity.SetTag(
            "exception.message",
            exception.Message);
    }
}