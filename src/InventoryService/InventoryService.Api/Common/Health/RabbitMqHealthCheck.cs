using InventoryService.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InventoryService.Api.Common.Health;

public sealed class RabbitMqHealthCheck(
    IRabbitMqConnectionFactory connectionFactory,
    ILogger<RabbitMqHealthCheck> logger) : IHealthCheck
{
    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly ILogger<RabbitMqHealthCheck> _logger = logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy("RabbitMQ is reachable.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "RabbitMQ health check failed.");

            return HealthCheckResult.Unhealthy(
                description: "RabbitMQ is not reachable.",
                exception: exception);
        }
    }
}