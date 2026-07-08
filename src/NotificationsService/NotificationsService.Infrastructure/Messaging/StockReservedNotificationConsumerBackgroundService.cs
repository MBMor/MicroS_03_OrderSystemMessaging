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
using Observability.Shared.Correlation;
using Observability.Shared.Messaging;

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
        _logger.LogInformation(
            "Notifications StockReserved consumer is starting. QueueName: {QueueName}, PrefetchCount: {PrefetchCount}",
            _topologyOptions.StockReservedQueueName,
            _consumerOptions.PrefetchCount);

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
            "Notifications StockReserved consumer is consuming queue '{QueueName}' with prefetch count {PrefetchCount}.",
            _topologyOptions.StockReservedQueueName,
            _consumerOptions.PrefetchCount);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        var fallbackCorrelationId = RabbitMqMessageHeaders.GetCorrelationIdOrCreate(
            eventArgs.BasicProperties.Headers);

        try
        {
            var command = CreateCommand(eventArgs);

            var correlationId = RabbitMqMessageHeaders.ResolveCorrelationId(
                eventArgs.BasicProperties.Headers,
                command.CorrelationId);

            command = command with
            {
                CorrelationId = correlationId
            };

            using var correlationScope = CorrelationIdLogScope.Begin(
                _logger,
                correlationId);

            _logger.LogInformation(
                "StockReserved notification message received. DeliveryTag: {DeliveryTag}, MessageId: {MessageId}, EventType: {EventType}, RoutingKey: {RoutingKey}, Redelivered: {Redelivered}, CorrelationId: {CorrelationId}",
                eventArgs.DeliveryTag,
                eventArgs.BasicProperties.MessageId,
                eventArgs.BasicProperties.Type,
                eventArgs.RoutingKey,
                eventArgs.Redelivered,
                correlationId);

            _logger.LogInformation(
                "StockReserved notification message {MessageId} deserialized. EventType: {EventType}, CorrelationId: {CorrelationId}, OrderId: {OrderId}, DeliveryTag: {DeliveryTag}",
                command.MessageId,
                command.EventType,
                command.CorrelationId,
                command.OrderId,
                eventArgs.DeliveryTag);

            await using var scope = _serviceScopeFactory.CreateAsyncScope();

            var correlationIdAccessor = scope.ServiceProvider
                .GetRequiredService<ICorrelationIdAccessor>();

            correlationIdAccessor.CorrelationId = correlationId;

            var service = scope.ServiceProvider
                .GetRequiredService<IEventNotificationService>();

            await service.HandleStockReservedAsync(
                command,
                cancellationToken);

            await channel.BasicAckAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "StockReserved notification message {MessageId} for order {OrderId} was processed and acknowledged. DeliveryTag: {DeliveryTag}, EventType: {EventType}, QueueName: {QueueName}, CorrelationId: {CorrelationId}",
                command.MessageId,
                command.OrderId,
                eventArgs.DeliveryTag,
                command.EventType,
                _topologyOptions.StockReservedQueueName,
                correlationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            using var correlationScope = CorrelationIdLogScope.Begin(
                _logger,
                fallbackCorrelationId);

            _logger.LogError(
                exception,
                "StockReserved notification message failed and will be dead-lettered. DeliveryTag: {DeliveryTag}, MessageId: {MessageId}, EventType: {EventType}, RoutingKey: {RoutingKey}, QueueName: {QueueName}, Redelivered: {Redelivered}, CorrelationId: {CorrelationId}",
                eventArgs.DeliveryTag,
                eventArgs.BasicProperties.MessageId,
                eventArgs.BasicProperties.Type,
                eventArgs.RoutingKey,
                _topologyOptions.StockReservedQueueName,
                eventArgs.Redelivered,
                fallbackCorrelationId);

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