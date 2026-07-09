using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Observability.Shared.Configuration;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Observability.Shared.OpenTelemetry;

public static class OpenTelemetryServiceCollectionExtensions
{
    private const string DefaultServiceVersion = "1.0.0";
    private const string ServiceDisplayNameAttributeName = "service.display_name";
    private const string DeploymentEnvironmentAttributeName = "deployment.environment";

    public static IServiceCollection AddOrderSystemOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        string serviceName,
        string serviceDisplayName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceDisplayName);

        var observabilityOptions = configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        var resolvedServiceName = GetValueOrDefault(
            observabilityOptions.ServiceName,
            serviceName);

        var resolvedServiceDisplayName = GetValueOrDefault(
            observabilityOptions.ServiceDisplayName,
            serviceDisplayName);

        var resolvedServiceVersion = GetValueOrDefault(
            observabilityOptions.ServiceVersion,
            DefaultServiceVersion);

        var resolvedEnvironmentName = GetValueOrDefault(
            observabilityOptions.EnvironmentName,
            hostEnvironment.EnvironmentName);

        services
            .AddOpenTelemetry()
            .ConfigureResource(resourceBuilder =>
            {
                resourceBuilder
                    .AddService(
                        serviceName: resolvedServiceName,
                        serviceVersion: resolvedServiceVersion)
                    .AddAttributes(
                    [
                        new KeyValuePair<string, object>(
                            ServiceDisplayNameAttributeName,
                            resolvedServiceDisplayName),

                        new KeyValuePair<string, object>(
                            DeploymentEnvironmentAttributeName,
                            resolvedEnvironmentName)
                    ]);
            })
            .WithTracing(_ =>
            {
                // Instrumentations will be added in later 
                // custom ActivitySource instrumentation.
            })
            .WithMetrics(_ =>
            {
                // Instrumentations will be added in later.
                // custom Meter/business metrics.
            });

        return services;
    }

    private static string GetValueOrDefault(
        string? value,
        string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : value.Trim();
    }
}