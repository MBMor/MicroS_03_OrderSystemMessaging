using Microsoft.Extensions.Logging;

namespace Observability.Shared.Correlation;

public static class CorrelationIdLogScope
{
    private static readonly Func<ILogger, string, IDisposable?> Scope =
        LoggerMessage.DefineScope<string>("CorrelationId:{CorrelationId}");

    public static IDisposable? Begin(
        ILogger logger,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return Scope(logger, correlationId);
    }
}