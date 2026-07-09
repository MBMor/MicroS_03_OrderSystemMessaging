namespace Observability.Shared.Configuration;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string? ServiceName { get; init; }

    public string? ServiceDisplayName { get; init; }

    public string? ServiceVersion { get; init; }

    public string? EnvironmentName { get; init; }
}