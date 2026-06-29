using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NotificationsService.Infrastructure.Messaging;

public sealed class RabbitMqTopologyInitializerBackgroundService : BackgroundService
{
    private readonly IRabbitMqTopologyInitializer _topologyInitializer;
    private readonly RabbitMqTopologyOptions _topologyOptions;
    private readonly ILogger<RabbitMqTopologyInitializerBackgroundService> _logger;

    public RabbitMqTopologyInitializerBackgroundService(
        IRabbitMqTopologyInitializer topologyInitializer,
        IOptions<RabbitMqTopologyOptions> topologyOptions,
        ILogger<RabbitMqTopologyInitializerBackgroundService> logger)
    {
        _topologyInitializer = topologyInitializer;
        _topologyOptions = topologyOptions.Value;
        _logger = logger;
    }

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