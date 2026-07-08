using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderSystem.Contracts.IntegrationEvents;
using OrdersService.Application.StockReservations.Abstractions;
using OrdersService.Application.StockReservations.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrdersService.Infrastructure.Messaging;

public sealed class StockReservedConsumerBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IRabbitMqConnectionFactory connectionFactory,
    IRabbitMqTopologyInitializer topologyInitializer,
    IOptions<RabbitMqTopologyOptions> topologyOptions,
    IOptions<StockReservationResultConsumerOptions> consumerOptions,
    ILogger<StockReservedConsumerBackgroundService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer = topologyInitializer;
    private readonly RabbitMqTopologyOptions _topologyOptions = topologyOptions.Value;
    private readonly StockReservationResultConsumerOptions _consumerOptions = consumerOptions.Value;
    private readonly ILogger<StockReservedConsumerBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Orders StockReserved consumer is starting. QueueName: {QueueName}, PrefetchCount: {PrefetchCount}",
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
                    "Orders StockReserved consumer failed. Retrying in {RetryDelaySeconds} second(s).",
                    _consumerOptions.ConnectionRetryDelaySeconds);

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_consumerOptions.ConnectionRetryDelaySeconds),
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Orders StockReserved consumer is stopping.");
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
            await HandleMessageAsync(
                channel,
                eventArgs,
                stoppingToken);
        };

        await channel.BasicConsumeAsync(
            queue: _topologyOptions.StockReservedQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Orders StockReserved consumer is consuming queue '{QueueName}' with prefetch count {PrefetchCount}.",
            _topologyOptions.StockReservedQueueName,
            _consumerOptions.PrefetchCount);

        await Task.Delay(
            Timeout.InfiniteTimeSpan,
            stoppingToken);
    }

    private async Task HandleMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "StockReserved message received. DeliveryTag: {DeliveryTag}, MessageId: {MessageId}, EventType: {EventType}, RoutingKey: {RoutingKey}, Redelivered: {Redelivered}",
            eventArgs.DeliveryTag,
            eventArgs.BasicProperties.MessageId,
            eventArgs.BasicProperties.Type,
            eventArgs.RoutingKey,
            eventArgs.Redelivered);

        try
        {
            var command = CreateCommand(eventArgs);

            _logger.LogInformation(
                "StockReserved message {MessageId} deserialized. EventType: {EventType}, CorrelationId: {CorrelationId}, OrderId: {OrderId}, DeliveryTag: {DeliveryTag}",
                command.MessageId,
                command.EventType,
                command.CorrelationId,
                command.OrderId,
                eventArgs.DeliveryTag);

            await using var scope = _serviceScopeFactory.CreateAsyncScope();

            var service = scope.ServiceProvider
                .GetRequiredService<IOrderStockReservationResultService>();

            await service.HandleStockReservedAsync(
                command,
                cancellationToken);

            await channel.BasicAckAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "StockReserved message {MessageId} for order {OrderId} was processed and acknowledged. DeliveryTag: {DeliveryTag}, EventType: {EventType}, QueueName: {QueueName}",
                command.MessageId,
                command.OrderId,
                eventArgs.DeliveryTag,
                command.EventType,
                _topologyOptions.StockReservedQueueName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "StockReserved message failed and will be dead-lettered. DeliveryTag: {DeliveryTag}, MessageId: {MessageId}, EventType: {EventType}, RoutingKey: {RoutingKey}, QueueName: {QueueName}, Redelivered: {Redelivered}",
                eventArgs.DeliveryTag,
                eventArgs.BasicProperties.MessageId,
                eventArgs.BasicProperties.Type,
                eventArgs.RoutingKey,
                _topologyOptions.StockReservedQueueName,
                eventArgs.Redelivered);

            await channel.BasicNackAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: cancellationToken);
        }
    }

    private static MarkOrderStockReservedCommand CreateCommand(
        BasicDeliverEventArgs eventArgs)
    {
        var json = Encoding.UTF8.GetString(eventArgs.Body.Span);

        var stockReserved = JsonSerializer.Deserialize<StockReserved>(
            json,
            JsonSerializerOptions);

        if (stockReserved is null)
        {
            throw new JsonException("StockReserved message payload could not be deserialized.");
        }

        return new MarkOrderStockReservedCommand
        {
            MessageId = GetMessageId(eventArgs, stockReserved),
            EventType = stockReserved.EventType,
            CorrelationId = stockReserved.CorrelationId,
            OrderId = stockReserved.OrderId
        };
    }

    private static Guid GetMessageId(
        BasicDeliverEventArgs eventArgs,
        StockReserved stockReserved)
    {
        var messageId = eventArgs.BasicProperties.MessageId;

        if (!string.IsNullOrWhiteSpace(messageId)
            && Guid.TryParse(messageId, out var parsedMessageId))
        {
            return parsedMessageId;
        }

        return stockReserved.EventId;
    }
}