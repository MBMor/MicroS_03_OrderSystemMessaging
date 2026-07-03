using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApiGateway.Health;

public sealed class UrlHealthCheck(HttpClient httpClient, string url) : IHealthCheck
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _url = url;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(_url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Endpoint '{_url}' is healthy.");
            }

            return HealthCheckResult.Unhealthy(
                $"Endpoint '{_url}' returned status code {(int)response.StatusCode}.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                $"Endpoint '{_url}' is not reachable.",
                exception);
        }
    }
}