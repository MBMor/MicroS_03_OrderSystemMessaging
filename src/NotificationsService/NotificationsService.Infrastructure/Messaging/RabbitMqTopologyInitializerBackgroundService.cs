using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NotificationsService.Infrastructure.Messaging;

public sealed class RabbitMqTopologyInitializerBackgroundService(
    IRabbitMqTopologyInitializer topologyInitializer,
    IOptions<RabbitMqTopologyOptions> topologyOptions,
    ILogger<RabbitMqTopologyInitializerBackgroundService> logger) : BackgroundService
{
    private readonly IRabbitMqTopologyInitializer _topologyInitializer = topologyInitializer;
    private readonly RabbitMqTopologyOptions _topologyOptions = topologyOptions.Value;
    private readonly ILogger<RabbitMqTopologyInitializerBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notifications RabbitMQ topology initializer is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _topologyInitializer.InitializeAsync(stoppingToken);

                _logger.LogInformation("Notifications RabbitMQ topology initializer completed successfully.");

                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Notifications RabbitMQ topology initialization failed. Retrying in {RetryDelaySeconds} second(s).",
                    _topologyOptions.InitializationRetryDelaySeconds);

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_topologyOptions.InitializationRetryDelaySeconds),
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}