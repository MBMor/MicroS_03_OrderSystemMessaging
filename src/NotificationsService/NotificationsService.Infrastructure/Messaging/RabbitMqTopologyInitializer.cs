using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace NotificationsService.Infrastructure.Messaging;

public sealed class RabbitMqTopologyInitializer(
    IRabbitMqConnectionFactory connectionFactory,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    IOptions<RabbitMqTopologyOptions> topologyOptions,
    ILogger<RabbitMqTopologyInitializer> logger) : IRabbitMqTopologyInitializer
{
    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly RabbitMqOptions _rabbitMqOptions = rabbitMqOptions.Value;
    private readonly RabbitMqTopologyOptions _topologyOptions = topologyOptions.Value;
    private readonly ILogger<RabbitMqTopologyInitializer> _logger = logger;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await DeclareExchangesAsync(channel, cancellationToken);
        await DeclareOrderCreatedQueuesAsync(channel, cancellationToken);
        await DeclareStockReservedQueuesAsync(channel, cancellationToken);
        await DeclareStockReservationFailedQueuesAsync(channel, cancellationToken);

        _logger.LogInformation(
            "Notifications RabbitMQ topology has been initialized. Exchange: '{ExchangeName}', Queues: '{OrderCreatedQueueName}', '{StockReservedQueueName}', '{StockReservationFailedQueueName}'.",
            _rabbitMqOptions.ExchangeName,
            _topologyOptions.OrderCreatedQueueName,
            _topologyOptions.StockReservedQueueName,
            _topologyOptions.StockReservationFailedQueueName);
    }

    private async Task DeclareExchangesAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchange: _rabbitMqOptions.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _topologyOptions.DeadLetterExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    private async Task DeclareOrderCreatedQueuesAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _topologyOptions.DeadLetterExchangeName,
            ["x-dead-letter-routing-key"] = RabbitMqRoutingKeys.NotificationsOrderCreatedDeadLetter
        };

        await channel.QueueDeclareAsync(
            queue: _topologyOptions.OrderCreatedQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _topologyOptions.OrderCreatedDeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _topologyOptions.OrderCreatedQueueName,
            exchange: _rabbitMqOptions.ExchangeName,
            routingKey: RabbitMqRoutingKeys.OrderCreated,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _topologyOptions.OrderCreatedDeadLetterQueueName,
            exchange: _topologyOptions.DeadLetterExchangeName,
            routingKey: RabbitMqRoutingKeys.NotificationsOrderCreatedDeadLetter,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    private async Task DeclareStockReservedQueuesAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _topologyOptions.DeadLetterExchangeName,
            ["x-dead-letter-routing-key"] = RabbitMqRoutingKeys.NotificationsStockReservedDeadLetter
        };

        await channel.QueueDeclareAsync(
            queue: _topologyOptions.StockReservedQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _topologyOptions.StockReservedDeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _topologyOptions.StockReservedQueueName,
            exchange: _rabbitMqOptions.ExchangeName,
            routingKey: RabbitMqRoutingKeys.StockReserved,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _topologyOptions.StockReservedDeadLetterQueueName,
            exchange: _topologyOptions.DeadLetterExchangeName,
            routingKey: RabbitMqRoutingKeys.NotificationsStockReservedDeadLetter,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    private async Task DeclareStockReservationFailedQueuesAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _topologyOptions.DeadLetterExchangeName,
            ["x-dead-letter-routing-key"] = RabbitMqRoutingKeys.NotificationsStockReservationFailedDeadLetter
        };

        await channel.QueueDeclareAsync(
            queue: _topologyOptions.StockReservationFailedQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _topologyOptions.StockReservationFailedDeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _topologyOptions.StockReservationFailedQueueName,
            exchange: _rabbitMqOptions.ExchangeName,
            routingKey: RabbitMqRoutingKeys.StockReservationFailed,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _topologyOptions.StockReservationFailedDeadLetterQueueName,
            exchange: _topologyOptions.DeadLetterExchangeName,
            routingKey: RabbitMqRoutingKeys.NotificationsStockReservationFailedDeadLetter,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}