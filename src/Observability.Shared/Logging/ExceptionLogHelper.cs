namespace Observability.Shared.Logging;

public static class ExceptionLogHelper
{
    public static string GetErrorType(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.GetType().FullName
            ?? exception.GetType().Name;
    }
}