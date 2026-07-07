namespace Observability.Shared.Correlation;

public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; set; }
}