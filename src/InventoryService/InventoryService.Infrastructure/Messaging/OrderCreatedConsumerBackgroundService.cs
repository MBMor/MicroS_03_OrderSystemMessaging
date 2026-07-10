using System.Diagnostics;
using System.Text;
using System.Text.Json;
using InventoryService.Application.StockReservations.Abstractions;
using InventoryService.Application.StockReservations.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Observability.Shared.Correlation;
using Observability.Shared.Messaging;
using Observability.Shared.Tracing;
using OpenTelemetry;
using OrderSystem.Contracts.IntegrationEvents;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Observability.Shared.Metrics;

namespace InventoryService.Infrastructure.Messaging;

public sealed class OrderCreatedConsumerBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IRabbitMqConnectionFactory connectionFactory,
    IRabbitMqTopologyInitializer topologyInitializer,
    IOptions<RabbitMqTopologyOptions> topologyOptions,
    IOptions<OrderCreatedConsumerOptions> consumerOptions,
    ILogger<OrderCreatedConsumerBackgroundService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer = topologyInitializer;
    private readonly RabbitMqTopologyOptions _topologyOptions = topologyOptions.Value;
    private readonly OrderCreatedConsumerOptions _consumerOptions = consumerOptions.Value;
    private readonly ILogger<OrderCreatedConsumerBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Inventory OrderCreated consumer is starting. QueueName: {QueueName}, PrefetchCount: {PrefetchCount}",
            _topologyOptions.OrderCreatedQueueName,
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
                    "Inventory OrderCreated consumer failed. Retrying in {RetryDelaySeconds} second(s).",
                    _consumerOptions.ConnectionRetryDelaySeconds);

                await Task.Delay(
                    TimeSpan.FromSeconds(_consumerOptions.ConnectionRetryDelaySeconds),
                    stoppingToken);
            }
        }

        _logger.LogInformation("Inventory OrderCreated consumer is stopping.");
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await _topologyInitializer.InitializeAsync(stoppingToken);

        await using var connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: (ushort)_consumerOptions.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            await HandleMessageAsync(
                channel,
                eventArgs,
                stoppingToken);
        };

        await channel.BasicConsumeAsync(
            queue: _topologyOptions.OrderCreatedQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Inventory OrderCreated consumer is consuming queue '{QueueName}' with prefetch count {PrefetchCount}.",
            _topologyOptions.OrderCreatedQueueName,
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

        var previousBaggage = Baggage.Current;

        Activity? consumeActivity = null;

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

            var parentContext = RabbitMqTraceContextHeaders.Extract(
                eventArgs.BasicProperties.Headers);

            Baggage.Current = parentContext.Baggage;

            using var correlationScope = CorrelationIdLogScope.Begin(
                _logger,
                correlationId);

            consumeActivity = StartConsumeActivity(
                eventArgs,
                _topologyOptions.OrderCreatedQueueName,
                correlationId,
                parentContext.ActivityContext);

            _logger.LogInformation(
                "OrderCreated message received. DeliveryTag: {DeliveryTag}, MessageId: {MessageId}, EventType: {EventType}, RoutingKey: {RoutingKey}, Redelivered: {Redelivered}, CorrelationId: {CorrelationId}",
                eventArgs.DeliveryTag,
                eventArgs.BasicProperties.MessageId,
                eventArgs.BasicProperties.Type,
                eventArgs.RoutingKey,
                eventArgs.Redelivered,
                correlationId);

            _logger.LogInformation(
                "OrderCreated message {MessageId} deserialized. EventType: {EventType}, CorrelationId: {CorrelationId}, OrderId: {OrderId}, ItemCount: {ItemCount}, DeliveryTag: {DeliveryTag}",
                command.MessageId,
                command.EventType,
                command.CorrelationId,
                command.OrderId,
                command.Items?.Count ?? 0,
                eventArgs.DeliveryTag);

            await using var scope = _serviceScopeFactory.CreateAsyncScope();

            var correlationIdAccessor = scope.ServiceProvider
                .GetRequiredService<ICorrelationIdAccessor>();

            correlationIdAccessor.CorrelationId = correlationId;

            var stockReservationService = scope.ServiceProvider
                .GetRequiredService<IStockReservationService>();

            await stockReservationService.HandleOrderCreatedAsync(
                command,
                cancellationToken);

            await channel.BasicAckAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken: cancellationToken);

            OrderSystemMessagingMetrics.RecordConsumed(
                _topologyOptions.OrderCreatedQueueName,
                eventArgs.RoutingKey,
                command.EventType);

            consumeActivity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation(
                "OrderCreated message {MessageId} for order {OrderId} was processed and acknowledged. DeliveryTag: {DeliveryTag}, EventType: {EventType}, QueueName: {QueueName}, CorrelationId: {CorrelationId}",
                command.MessageId,
                command.OrderId,
                eventArgs.DeliveryTag,
                command.EventType,
                _topologyOptions.OrderCreatedQueueName,
                correlationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var fallbackParentContext = RabbitMqTraceContextHeaders.Extract(
                eventArgs.BasicProperties.Headers);

            consumeActivity ??= StartConsumeActivity(
                eventArgs,
                _topologyOptions.OrderCreatedQueueName,
                fallbackCorrelationId,
                fallbackParentContext.ActivityContext);

            consumeActivity.SetError(exception);

            using var correlationScope = CorrelationIdLogScope.Begin(
                _logger,
                fallbackCorrelationId);

            _logger.LogError(
                exception,
                "OrderCreated message failed and will be dead-lettered. DeliveryTag: {DeliveryTag}, MessageId: {MessageId}, EventType: {EventType}, RoutingKey: {RoutingKey}, QueueName: {QueueName}, Redelivered: {Redelivered}, CorrelationId: {CorrelationId}",
                eventArgs.DeliveryTag,
                eventArgs.BasicProperties.MessageId,
                eventArgs.BasicProperties.Type,
                eventArgs.RoutingKey,
                _topologyOptions.OrderCreatedQueueName,
                eventArgs.Redelivered,
                fallbackCorrelationId);

            await channel.BasicNackAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: cancellationToken);
        }
        finally
        {
            consumeActivity?.Dispose();
            Baggage.Current = previousBaggage;
        }
    }

    private static ReserveStockForOrderCommand CreateCommand(
        BasicDeliverEventArgs eventArgs)
    {
        var json = Encoding.UTF8.GetString(eventArgs.Body.Span);

        var integrationEvent = JsonSerializer.Deserialize<OrderCreated>(
            json,
            JsonSerializerOptions);

        if (integrationEvent is null)
        {
            throw new InvalidOperationException("OrderCreated message payload could not be deserialized.");
        }

        return new ReserveStockForOrderCommand
        {
            MessageId = integrationEvent.EventId,
            EventType = integrationEvent.EventType,
            CorrelationId = integrationEvent.CorrelationId,
            OrderId = integrationEvent.OrderId,
            CustomerName = integrationEvent.CustomerName,
            CustomerEmail = integrationEvent.CustomerEmail,
            Items = integrationEvent.Items
                .Select(item => new ReserveStockForOrderItemCommand
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                })
                .ToList()
        };
    }

    private static Activity? StartConsumeActivity(
        BasicDeliverEventArgs eventArgs,
        string queueName,
        string correlationId,
        ActivityContext parentContext)
    {
        var activity = parentContext.TraceId != default
            ? OrderSystemActivitySources.Messaging.StartActivity(
                "rabbitmq.consume",
                ActivityKind.Consumer,
                parentContext)
            : OrderSystemActivitySources.Messaging.StartActivity(
                "rabbitmq.consume",
                ActivityKind.Consumer);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingSystem,
            "rabbitmq");

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingOperation,
            "consume");

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingMessageId,
            eventArgs.BasicProperties.MessageId);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.EventType,
            eventArgs.BasicProperties.Type);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingRabbitMqQueueName,
            queueName);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingRabbitMqRoutingKey,
            eventArgs.RoutingKey);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingRabbitMqDeliveryTag,
            eventArgs.DeliveryTag);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingRabbitMqRedelivered,
            eventArgs.Redelivered);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.CorrelationId,
            correlationId);

        return activity;
    }
}