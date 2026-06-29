using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationsService.Application.EventNotifications.Abstractions;
using NotificationsService.Application.EventNotifications.Contracts;
using OrderSystem.Contracts.IntegrationEvents;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationsService.Infrastructure.Messaging;

public sealed class StockReservedNotificationConsumerBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IRabbitMqConnectionFactory connectionFactory,
    IRabbitMqTopologyInitializer topologyInitializer,
    IOptions<RabbitMqTopologyOptions> topologyOptions,
    IOptions<EventNotificationConsumerOptions> consumerOptions,
    ILogger<StockReservedNotificationConsumerBackgroundService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer = topologyInitializer;
    private readonly RabbitMqTopologyOptions _topologyOptions = topologyOptions.Value;
    private readonly EventNotificationConsumerOptions _consumerOptions = consumerOptions.Value;
    private readonly ILogger<StockReservedNotificationConsumerBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notifications StockReserved consumer is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Notifications StockReserved consumer failed. Retrying in {RetryDelaySeconds} second(s).",
                    _consumerOptions.ConnectionRetryDelaySeconds);

                await Task.Delay(
                    TimeSpan.FromSeconds(_consumerOptions.ConnectionRetryDelaySeconds),
                    stoppingToken);
            }
        }

        _logger.LogInformation("Notifications StockReserved consumer is stopping.");
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await _topologyInitializer.InitializeAsync(stoppingToken);

        await using var connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _consumerOptions.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            await HandleMessageAsync(channel, eventArgs, stoppingToken);
        };

        await channel.BasicConsumeAsync(
            queue: _topologyOptions.StockReservedQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Notifications StockReserved consumer is consuming queue '{QueueName}'.",
            _topologyOptions.StockReservedQueueName);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = CreateCommand(eventArgs);

            await using var scope = _serviceScopeFactory.CreateAsyncScope();

            var service = scope.ServiceProvider
                .GetRequiredService<IEventNotificationService>();

            await service.HandleStockReservedAsync(command, cancellationToken);

            await channel.BasicAckAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "StockReserved notification message {MessageId} for order {OrderId} was processed.",
                command.MessageId,
                command.OrderId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "StockReserved notification message delivery tag {DeliveryTag} failed and will be dead-lettered.",
                eventArgs.DeliveryTag);

            await channel.BasicNackAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: cancellationToken);
        }
    }

    private static CreateStockReservedNotificationCommand CreateCommand(
        BasicDeliverEventArgs eventArgs)
    {
        var json = Encoding.UTF8.GetString(eventArgs.Body.Span);

        var integrationEvent = JsonSerializer.Deserialize<StockReserved>(
            json,
            JsonSerializerOptions);

        if (integrationEvent is null)
        {
            throw new JsonException("StockReserved message payload could not be deserialized.");
        }

        return new CreateStockReservedNotificationCommand
        {
            MessageId = GetMessageId(eventArgs, integrationEvent.EventId),
            EventType = integrationEvent.EventType,
            CorrelationId = integrationEvent.CorrelationId,
            OrderId = integrationEvent.OrderId,
            CustomerName = integrationEvent.CustomerName,
            CustomerEmail = integrationEvent.CustomerEmail
        };
    }

    private static Guid GetMessageId(
        BasicDeliverEventArgs eventArgs,
        Guid fallbackEventId)
    {
        var messageId = eventArgs.BasicProperties.MessageId;

        if (!string.IsNullOrWhiteSpace(messageId)
            && Guid.TryParse(messageId, out var parsedMessageId))
        {
            return parsedMessageId;
        }

        return fallbackEventId;
    }
}