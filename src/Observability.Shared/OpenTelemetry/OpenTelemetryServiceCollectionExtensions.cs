using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Observability.Shared.Configuration;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Routing;

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
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = httpContext =>
                            !IsHealthCheckRequest(httpContext);

                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            var normalizedRoute = GetNormalizedHttpRoute(
                                response.HttpContext);

                            if (normalizedRoute is null)
                            {
                                return;
                            }

                            activity.DisplayName =
                                $"{response.HttpContext.Request.Method} {normalizedRoute}";

                            activity.SetTag(
                                "http.route",
                                normalizedRoute);
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter();
            });

        return services;
    }

    private static bool IsHealthCheckRequest(HttpContext httpContext)
    {
        return httpContext.Request.Path.StartsWithSegments(
            "/health",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetNormalizedHttpRoute(HttpContext httpContext)
    {
        if (httpContext.GetEndpoint() is not RouteEndpoint routeEndpoint)
        {
            return null;
        }

        var routePattern = routeEndpoint.RoutePattern.RawText;

        if (string.IsNullOrWhiteSpace(routePattern))
        {
            return null;
        }

        return ReplaceApiVersionToken(
            routePattern,
            httpContext);
    }

    private static string ReplaceApiVersionToken(
        string routePattern,
        HttpContext httpContext)
    {
        if (!httpContext.Request.RouteValues.TryGetValue(
                "version",
                out var versionValue))
        {
            return routePattern;
        }

        var version = versionValue?.ToString();

        if (string.IsNullOrWhiteSpace(version))
        {
            return routePattern;
        }

        return routePattern
            .Replace(
                "{version:apiVersion}",
                version,
                StringComparison.OrdinalIgnoreCase)
            .Replace(
                "{version}",
                version,
                StringComparison.OrdinalIgnoreCase);
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