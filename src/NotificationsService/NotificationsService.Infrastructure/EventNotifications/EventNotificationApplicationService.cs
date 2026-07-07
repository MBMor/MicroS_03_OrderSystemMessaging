using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationsService.Application.Common.Abstractions;
using NotificationsService.Application.EventNotifications.Abstractions;
using NotificationsService.Application.EventNotifications.Contracts;
using NotificationsService.Domain.Notifications;
using NotificationsService.Infrastructure.Idempotency;
using NotificationsService.Infrastructure.Messaging;
using NotificationsService.Infrastructure.Persistence;

namespace NotificationsService.Infrastructure.EventNotifications;

public sealed class EventNotificationApplicationService(
    NotificationsDbContext dbContext,
    IClock clock,
    IValidator<CreateOrderCreatedNotificationCommand> orderCreatedValidator,
    IValidator<CreateStockReservedNotificationCommand> stockReservedValidator,
    IValidator<CreateStockReservationFailedNotificationCommand> stockReservationFailedValidator,
    ILogger<EventNotificationApplicationService> logger) : IEventNotificationService
{
    private readonly NotificationsDbContext _dbContext = dbContext;
    private readonly IClock _clock = clock;
    private readonly IValidator<CreateOrderCreatedNotificationCommand> _orderCreatedValidator = orderCreatedValidator;
    private readonly IValidator<CreateStockReservedNotificationCommand> _stockReservedValidator = stockReservedValidator;
    private readonly IValidator<CreateStockReservationFailedNotificationCommand> _stockReservationFailedValidator = stockReservationFailedValidator;
    private readonly ILogger<EventNotificationApplicationService> _logger = logger;

    public async Task HandleOrderCreatedAsync(
        CreateOrderCreatedNotificationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationResult = await _orderCreatedValidator.ValidateAsync(
            command,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var alreadyProcessed = await IsAlreadyProcessedAsync(
            command.MessageId,
            command.EventType!,
            ConsumerNames.NotificationsOrderCreatedConsumer,
            cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Duplicate notification message {MessageId} skipped by {ConsumerName}",
                command.MessageId,
                ConsumerNames.NotificationsOrderCreatedConsumer);

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var notificationCreated = await CreateNotificationIfMissingAsync(
            sourceEventId: command.MessageId,
            sourceEventType: command.EventType!,
            recipient: command.CustomerEmail!,
            subject: $"Order {command.OrderId} was created",
            body: $"Hello {command.CustomerName}, your order {command.OrderId} was created and is waiting for stock reservation.",
            cancellationToken);

        if (notificationCreated)
        {
            _logger.LogInformation(
                "Order created notification prepared for order {OrderId}. SourceEventId: {SourceEventId}, SourceEventType: {SourceEventType}",
                command.OrderId,
                command.MessageId,
                command.EventType);
        }

        AddProcessedMessage(
            command.MessageId,
            command.EventType!,
            ConsumerNames.NotificationsOrderCreatedConsumer);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Order created notification processing completed for order {OrderId}. MessageId: {MessageId}",
            command.OrderId,
            command.MessageId);
    }

    public async Task HandleStockReservedAsync(
        CreateStockReservedNotificationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationResult = await _stockReservedValidator.ValidateAsync(
            command,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var alreadyProcessed = await IsAlreadyProcessedAsync(
            command.MessageId,
            command.EventType!,
            ConsumerNames.NotificationsStockReservedConsumer,
            cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Duplicate notification message {MessageId} skipped by {ConsumerName}",
                command.MessageId,
                ConsumerNames.NotificationsStockReservedConsumer);

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var notificationCreated = await CreateNotificationIfMissingAsync(
            sourceEventId: command.MessageId,
            sourceEventType: command.EventType!,
            recipient: command.CustomerEmail!,
            subject: $"Stock reserved for order {command.OrderId}",
            body: $"Hello {command.CustomerName}, stock has been successfully reserved for your order {command.OrderId}.",
            cancellationToken);

        if (notificationCreated)
        {
            _logger.LogInformation(
                "Stock reserved notification prepared for order {OrderId}. SourceEventId: {SourceEventId}, SourceEventType: {SourceEventType}",
                command.OrderId,
                command.MessageId,
                command.EventType);
        }

        AddProcessedMessage(
            command.MessageId,
            command.EventType!,
            ConsumerNames.NotificationsStockReservedConsumer);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Stock reserved notification processing completed for order {OrderId}. MessageId: {MessageId}",
            command.OrderId,
            command.MessageId);
    }

    public async Task HandleStockReservationFailedAsync(
        CreateStockReservationFailedNotificationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationResult = await _stockReservationFailedValidator.ValidateAsync(
            command,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var alreadyProcessed = await IsAlreadyProcessedAsync(
            command.MessageId,
            command.EventType!,
            ConsumerNames.NotificationsStockReservationFailedConsumer,
            cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Duplicate notification message {MessageId} skipped by {ConsumerName}",
                command.MessageId,
                ConsumerNames.NotificationsStockReservationFailedConsumer);

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var notificationCreated = await CreateNotificationIfMissingAsync(
            sourceEventId: command.MessageId,
            sourceEventType: command.EventType!,
            recipient: command.CustomerEmail!,
            subject: $"Stock reservation failed for order {command.OrderId}",
            body: $"Hello {command.CustomerName}, stock reservation failed for your order {command.OrderId}. Reason: {command.Reason}",
            cancellationToken);

        if (notificationCreated)
        {
            _logger.LogWarning(
                "Stock reservation failed notification prepared for order {OrderId}. SourceEventId: {SourceEventId}, SourceEventType: {SourceEventType}, Reason: {Reason}",
                command.OrderId,
                command.MessageId,
                command.EventType,
                command.Reason);
        }

        AddProcessedMessage(
            command.MessageId,
            command.EventType!,
            ConsumerNames.NotificationsStockReservationFailedConsumer);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Stock reservation failed notification processing completed for order {OrderId}. MessageId: {MessageId}",
            command.OrderId,
            command.MessageId);
    }

    private async Task<bool> IsAlreadyProcessedAsync(
        Guid messageId,
        string eventType,
        string consumerName,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ProcessedMessages
            .AnyAsync(
                processedMessage =>
                    processedMessage.MessageId == messageId
                    && processedMessage.EventType == eventType
                    && processedMessage.ConsumerName == consumerName,
                cancellationToken);
    }

    private async Task<bool> CreateNotificationIfMissingAsync(
        Guid sourceEventId,
        string sourceEventType,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var notificationAlreadyExists = await _dbContext.Notifications
            .AsNoTracking()
            .AnyAsync(
                notification =>
                    notification.SourceEventId == sourceEventId
                    && notification.SourceEventType == sourceEventType,
                cancellationToken);

        if (notificationAlreadyExists)
        {
            _logger.LogInformation(
                "Notification for source event {SourceEventId} and event type {SourceEventType} already exists",
                sourceEventId,
                sourceEventType);

            return false;
        }

        var notification = new Notification(
            id: Guid.NewGuid(),
            sourceEventId: sourceEventId,
            sourceEventType: sourceEventType,
            recipient: recipient,
            subject: subject,
            body: body,
            createdAtUtc: _clock.UtcNow);

        _dbContext.Notifications.Add(notification);

        _logger.LogInformation(
            "Notification {NotificationId} created for source event {SourceEventId} and event type {SourceEventType}",
            notification.Id,
            sourceEventId,
            sourceEventType);

        return true;
    }

    private void AddProcessedMessage(
        Guid messageId,
        string eventType,
        string consumerName)
    {
        var processedMessage = new ProcessedMessage(
            id: Guid.NewGuid(),
            messageId: messageId,
            eventType: eventType,
            consumerName: consumerName,
            processedAtUtc: _clock.UtcNow);

        _dbContext.ProcessedMessages.Add(processedMessage);
    }
}