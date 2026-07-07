namespace Observability.Shared.Correlation;

public static class CorrelationIdGenerator
{
    public static string Create()
    {
        return Guid.NewGuid().ToString("N");
    }
}