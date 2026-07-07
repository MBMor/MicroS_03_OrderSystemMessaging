namespace Observability.Shared.Correlation;

public static class CorrelationIdConstants
{
    public const string HeaderName = "X-Correlation-Id";

    public const string LogScopeKey = "CorrelationId";

    public const int MinLength = 8;

    public const int MaxLength = 128;
}