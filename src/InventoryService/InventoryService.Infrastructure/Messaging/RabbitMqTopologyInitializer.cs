using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace InventoryService.Infrastructure.Messaging;

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

        _logger.LogInformation(
            "Inventory RabbitMQ topology has been initialized. Exchange: '{ExchangeName}', Queue: '{QueueName}', DLQ: '{DeadLetterQueueName}'.",
            _rabbitMqOptions.ExchangeName,
            _topologyOptions.OrderCreatedQueueName,
            _topologyOptions.OrderCreatedDeadLetterQueueName);
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
            ["x-dead-letter-routing-key"] = RabbitMqRoutingKeys.InventoryOrderCreatedDeadLetter
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
            routingKey: RabbitMqRoutingKeys.InventoryOrderCreatedDeadLetter,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}