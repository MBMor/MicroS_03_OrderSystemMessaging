using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Observability.Shared.Messaging;
using Observability.Shared.Tracing;
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
        _logger.LogInformation(
            "Orders outbox publisher background service is starting. PollingIntervalSeconds: {PollingIntervalSeconds}, BatchSize: {BatchSize}, MaxRetryCount: {MaxRetryCount}, ExchangeName: {ExchangeName}",
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
            _logger.LogDebug("No pending Orders outbox messages found.");
            return;
        }

        using var activity = OrderSystemActivitySources.Outbox.StartActivity(
            "outbox.publish_batch",
            ActivityKind.Internal);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.OutboxBatchSize,
            outboxMessages.Count);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingDestinationName,
            _rabbitMqOptions.ExchangeName);

        try
        {
            _logger.LogInformation(
                "Publishing {OutboxMessageCount} Orders outbox message(s). BatchSize: {BatchSize}, ExchangeName: {ExchangeName}",
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

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            activity.SetError(exception);
            throw;
        }
    }

    private async Task PublishSingleMessageAsync(
        OrdersDbContext dbContext,
        IClock clock,
        IChannel channel,
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken)
    {
        var correlationId = RabbitMqMessageHeaders.GetCorrelationIdFromJsonPayload(
            outboxMessage.Payload);

        using var activity = OrderSystemActivitySources.Outbox.StartActivity(
            "outbox.publish_message",
            ActivityKind.Internal);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.OutboxMessageId,
            outboxMessage.Id);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.EventId,
            outboxMessage.EventId);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.EventType,
            outboxMessage.EventType);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingRabbitMqRoutingKey,
            outboxMessage.RoutingKey);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.OutboxRetryCount,
            outboxMessage.RetryCount);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.CorrelationId,
            correlationId);

        try
        {
            _logger.LogInformation(
                "Publishing Orders outbox message {OutboxMessageId}. EventId: {EventId}, EventType: {EventType}, RoutingKey: {RoutingKey}, RetryCount: {RetryCount}, ExchangeName: {ExchangeName}, CorrelationId: {CorrelationId}",
                outboxMessage.Id,
                outboxMessage.EventId,
                outboxMessage.EventType,
                outboxMessage.RoutingKey,
                outboxMessage.RetryCount,
                _rabbitMqOptions.ExchangeName,
                correlationId);

            await PublishToRabbitMqAsync(
                channel,
                outboxMessage,
                correlationId,
                cancellationToken);

            outboxMessage.MarkPublished(clock.UtcNow);

            await dbContext.SaveChangesAsync(cancellationToken);

            activity.SetTagIfNotNull(
                OrderSystemActivityTagNames.OutboxMessageStatus,
                outboxMessage.Status.ToString());

            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation(
                "Orders outbox message {OutboxMessageId} published. EventId: {EventId}, EventType: {EventType}, RoutingKey: {RoutingKey}, RetryCount: {RetryCount}, Status: {Status}, CorrelationId: {CorrelationId}",
                outboxMessage.Id,
                outboxMessage.EventId,
                outboxMessage.EventType,
                outboxMessage.RoutingKey,
                outboxMessage.RetryCount,
                outboxMessage.Status,
                correlationId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkPublishFailure(
                outboxMessage,
                exception,
                _outboxPublisherOptions.MaxRetryCount);

            await dbContext.SaveChangesAsync(cancellationToken);

            activity.SetTagIfNotNull(
                OrderSystemActivityTagNames.OutboxMessageStatus,
                outboxMessage.Status.ToString());

            activity.SetError(exception);

            var logLevel = outboxMessage.Status == OutboxStatus.Failed
                ? LogLevel.Error
                : LogLevel.Warning;

            _logger.Log(
                logLevel,
                exception,
                "Publishing Orders outbox message {OutboxMessageId} failed. EventId: {EventId}, EventType: {EventType}, RoutingKey: {RoutingKey}, RetryCount: {RetryCount}, MaxRetryCount: {MaxRetryCount}, Status: {Status}, CorrelationId: {CorrelationId}",
                outboxMessage.Id,
                outboxMessage.EventId,
                outboxMessage.EventType,
                outboxMessage.RoutingKey,
                outboxMessage.RetryCount,
                _outboxPublisherOptions.MaxRetryCount,
                outboxMessage.Status,
                correlationId);
        }
    }

    private async Task PublishToRabbitMqAsync(
        IChannel channel,
        OutboxMessage outboxMessage,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(outboxMessage.Payload);

        var headers = new Dictionary<string, object?>();

        RabbitMqMessageHeaders.SetCorrelationId(
            headers,
            correlationId);

        if (correlationId is null)
        {
            _logger.LogWarning(
                "Orders outbox message {OutboxMessageId} does not contain a valid correlation id in payload. RabbitMQ header {CorrelationIdHeaderName} will not be set.",
                outboxMessage.Id,
                RabbitMqMessageHeaders.CorrelationIdHeaderName);
        }

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = outboxMessage.EventId.ToString(),
            Type = outboxMessage.EventType,
            Headers = headers
        };

        using var activity = OrderSystemActivitySources.Messaging.StartActivity(
            "rabbitmq.publish",
            ActivityKind.Producer);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingSystem,
            "rabbitmq");

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingOperation,
            "publish");

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingDestinationName,
            _rabbitMqOptions.ExchangeName);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingRabbitMqRoutingKey,
            outboxMessage.RoutingKey);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.MessagingMessageId,
            outboxMessage.EventId);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.EventType,
            outboxMessage.EventType);

        activity.SetTagIfNotNull(
            OrderSystemActivityTagNames.CorrelationId,
            correlationId);

        try
        {
            await channel.BasicPublishAsync(
                exchange: _rabbitMqOptions.ExchangeName,
                routingKey: outboxMessage.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            activity.SetError(exception);
            throw;
        }
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