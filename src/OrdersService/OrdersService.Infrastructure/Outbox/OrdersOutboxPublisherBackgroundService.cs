using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrdersService.Application.Common.Abstractions;
using OrdersService.Infrastructure.Messaging;
using OrdersService.Infrastructure.Persistence;
using RabbitMQ.Client;

namespace OrdersService.Infrastructure.Outbox;

public sealed class OrdersOutboxPublisherBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IRabbitMqConnectionFactory connectionFactory,
    IRabbitMqTopologyInitializer topologyInitializer,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    IOptions<OutboxPublisherOptions> outboxPublisherOptions,
    ILogger<OrdersOutboxPublisherBackgroundService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer = topologyInitializer;
    private readonly RabbitMqOptions _rabbitMqOptions = rabbitMqOptions.Value;
    private readonly OutboxPublisherOptions _outboxPublisherOptions = outboxPublisherOptions.Value;
    private readonly ILogger<OrdersOutboxPublisherBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Orders outbox publisher background service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Orders outbox publisher iteration failed.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_outboxPublisherOptions.PollingIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Orders outbox publisher background service is stopping.");
    }

    private async Task PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var outboxMessages = await dbContext.OutboxMessages
            .Where(message => message.Status == OutboxStatus.Pending)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(_outboxPublisherOptions.BatchSize)
            .ToListAsync(cancellationToken);

        if (outboxMessages.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Publishing {OutboxMessageCount} Orders outbox message(s).",
            outboxMessages.Count);

        await _topologyInitializer.InitializeAsync(cancellationToken);

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            channelOptions,
            cancellationToken: cancellationToken);

        foreach (var outboxMessage in outboxMessages)
        {
            await PublishSingleMessageAsync(
                dbContext,
                clock,
                channel,
                outboxMessage,
                cancellationToken);
        }
    }

    private async Task PublishSingleMessageAsync(
        OrdersDbContext dbContext,
        IClock clock,
        IChannel channel,
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await PublishToRabbitMqAsync(
                channel,
                outboxMessage,
                cancellationToken);

            outboxMessage.MarkPublished(clock.UtcNow);

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Outbox message {OutboxMessageId} with event {EventId} was published using routing key '{RoutingKey}'.",
                outboxMessage.Id,
                outboxMessage.EventId,
                outboxMessage.RoutingKey);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkPublishFailure(
                outboxMessage,
                exception,
                _outboxPublisherOptions.MaxRetryCount);

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                exception,
                "Publishing outbox message {OutboxMessageId} with event {EventId} failed. RetryCount: {RetryCount}. Status: {Status}.",
                outboxMessage.Id,
                outboxMessage.EventId,
                outboxMessage.RetryCount,
                outboxMessage.Status);
        }
    }

    private async Task PublishToRabbitMqAsync(
        IChannel channel,
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(outboxMessage.Payload);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = outboxMessage.EventId.ToString(),
            Type = outboxMessage.EventType
        };

        await channel.BasicPublishAsync(
            exchange: _rabbitMqOptions.ExchangeName,
            routingKey: outboxMessage.RoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    private static void MarkPublishFailure(
        OutboxMessage outboxMessage,
        Exception exception,
        int maxRetryCount)
    {
        if (outboxMessage.RetryCount + 1 >= maxRetryCount)
        {
            outboxMessage.MarkFailed(exception.Message);
            return;
        }

        outboxMessage.MarkPendingForRetry(exception.Message);
    }
}