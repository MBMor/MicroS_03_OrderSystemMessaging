namespace Observability.Shared.Tracing;

public readonly record struct TraceContextSnapshot(
    string? TraceParent,
    string? TraceState)
{
    public static TraceContextSnapshot Empty => new(
        TraceParent: null,
        TraceState: null);

    public bool HasTraceParent => !string.IsNullOrWhiteSpace(TraceParent);
}