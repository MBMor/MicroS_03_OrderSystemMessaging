namespace Observability.Shared.Correlation;

public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    public string? CorrelationId { get; set; }
}