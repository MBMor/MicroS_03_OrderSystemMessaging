using System.Text;
using InventoryService.Application.Common.Abstractions;
using InventoryService.Infrastructure.Messaging;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace InventoryService.Infrastructure.Outbox;

public sealed class InventoryOutboxPublisherBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IRabbitMqConnectionFactory connectionFactory,
    IRabbitMqTopologyInitializer topologyInitializer,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    IOptions<OutboxPublisherOptions> outboxPublisherOptions,
    ILogger<InventoryOutboxPublisherBackgroundService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer = topologyInitializer;
    private readonly RabbitMqOptions _rabbitMqOptions = rabbitMqOptions.Value;
    private readonly OutboxPublisherOptions _outboxPublisherOptions = outboxPublisherOptions.Value;
    private readonly ILogger<InventoryOutboxPublisherBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Inventory outbox publisher background service is starting. PollingIntervalSeconds: {PollingIntervalSeconds}, BatchSize: {BatchSize}, MaxRetryCount: {MaxRetryCount}, ExchangeName: {ExchangeName}",
            _outboxPublisherOptions.PollingIntervalSeconds,
            _outboxPublisherOptions.BatchSize,
            _outboxPublisherOptions.MaxRetryCount,
            _rabbitMqOptions.ExchangeName);

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
                    "Inventory outbox publisher iteration failed.");
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

        _logger.LogInformation("Inventory outbox publisher background service is stopping.");
    }

    private async Task PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var outboxMessages = await dbContext.OutboxMessages
            .Where(message => message.Status == OutboxStatus.Pending)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(_outboxPublisherOptions.BatchSize)
            .ToListAsync(cancellationToken);

        if (outboxMessages.Count == 0)
        {
            _logger.LogDebug("No pending Inventory outbox messages found.");
            return;
        }

        _logger.LogInformation(
            "Publishing {OutboxMessageCount} Inventory outbox message(s). BatchSize: {BatchSize}, ExchangeName: {ExchangeName}",
            outboxMessages.Count,
            _outboxPublisherOptions.BatchSize,
            _rabbitMqOptions.ExchangeName);

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
        InventoryDbContext dbContext,
        IClock clock,
        IChannel channel,
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Publishing Inventory outbox message {OutboxMessageId}. EventId: {EventId}, EventType: {EventType}, RoutingKey: {RoutingKey}, RetryCount: {RetryCount}, ExchangeName: {ExchangeName}",
                outboxMessage.Id,
                outboxMessage.EventId,
                outboxMessage.EventType,
                outboxMessage.RoutingKey,
                outboxMessage.RetryCount,
                _rabbitMqOptions.ExchangeName);

            await PublishToRabbitMqAsync(
                channel,
                outboxMessage,
                cancellationToken);

            outboxMessage.MarkPublished(clock.UtcNow);

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Inventory outbox message {OutboxMessageId} published. EventId: {EventId}, EventType: {EventType}, RoutingKey: {RoutingKey}, RetryCount: {RetryCount}, Status: {Status}",
                outboxMessage.Id,
                outboxMessage.EventId,
                outboxMessage.EventType,
                outboxMessage.RoutingKey,
                outboxMessage.RetryCount,
                outboxMessage.Status);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkPublishFailure(
                outboxMessage,
                exception,
                _outboxPublisherOptions.MaxRetryCount);

            await dbContext.SaveChangesAsync(cancellationToken);

            var logLevel = outboxMessage.Status == OutboxStatus.Failed
                ? LogLevel.Error
                : LogLevel.Warning;

            _logger.Log(
                logLevel,
                exception,
                "Publishing Inventory outbox message {OutboxMessageId} failed. EventId: {EventId}, EventType: {EventType}, RoutingKey: {RoutingKey}, RetryCount: {RetryCount}, MaxRetryCount: {MaxRetryCount}, Status: {Status}",
                outboxMessage.Id,
                outboxMessage.EventId,
                outboxMessage.EventType,
                outboxMessage.RoutingKey,
                outboxMessage.RetryCount,
                _outboxPublisherOptions.MaxRetryCount,
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