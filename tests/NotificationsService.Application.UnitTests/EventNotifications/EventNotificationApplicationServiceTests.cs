using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NotificationsService.Application.EventNotifications.Contracts;
using NotificationsService.Application.EventNotifications.Validation;
using NotificationsService.Application.UnitTests.Common;
using NotificationsService.Domain.Notifications;
using NotificationsService.Infrastructure.EventNotifications;
using NotificationsService.Infrastructure.Messaging;
using NotificationsService.Infrastructure.Persistence;
using OrderSystem.Contracts.IntegrationEvents;

namespace NotificationsService.Application.UnitTests.EventNotifications;

public sealed class EventNotificationApplicationServiceTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleOrderCreatedAsync_WithValidCommand_CreatesNotificationAndProcessedMessage()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = CreateOrderCreatedCommand();

        await service.HandleOrderCreatedAsync(
            command,
            CancellationToken.None);

        var notification = await dbContext.Notifications.SingleAsync();

        Assert.Equal(command.MessageId, notification.SourceEventId);
        Assert.Equal(command.EventType, notification.SourceEventType);
        Assert.Equal(command.CustomerEmail, notification.Recipient);
        Assert.Contains(command.OrderId.ToString(), notification.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("was created", notification.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(NotificationStatus.Created, notification.Status);
        Assert.Equal(UtcNow, notification.CreatedAtUtc);

        var processedMessage = await dbContext.ProcessedMessages.SingleAsync();

        Assert.Equal(command.MessageId, processedMessage.MessageId);
        Assert.Equal(command.EventType, processedMessage.EventType);
        Assert.Equal(ConsumerNames.NotificationsOrderCreatedConsumer, processedMessage.ConsumerName);
        Assert.Equal(UtcNow, processedMessage.ProcessedAtUtc);
    }

    [Fact]
    public async Task HandleStockReservedAsync_WithValidCommand_CreatesNotificationAndProcessedMessage()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = CreateStockReservedCommand();

        await service.HandleStockReservedAsync(
            command,
            CancellationToken.None);

        var notification = await dbContext.Notifications.SingleAsync();

        Assert.Equal(command.MessageId, notification.SourceEventId);
        Assert.Equal(command.EventType, notification.SourceEventType);
        Assert.Equal(command.CustomerEmail, notification.Recipient);
        Assert.Contains(command.OrderId.ToString(), notification.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stock reserved", notification.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(NotificationStatus.Created, notification.Status);
        Assert.Equal(UtcNow, notification.CreatedAtUtc);

        var processedMessage = await dbContext.ProcessedMessages.SingleAsync();

        Assert.Equal(command.MessageId, processedMessage.MessageId);
        Assert.Equal(command.EventType, processedMessage.EventType);
        Assert.Equal(ConsumerNames.NotificationsStockReservedConsumer, processedMessage.ConsumerName);
        Assert.Equal(UtcNow, processedMessage.ProcessedAtUtc);
    }

    [Fact]
    public async Task HandleStockReservationFailedAsync_WithValidCommand_CreatesNotificationAndProcessedMessage()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = CreateStockReservationFailedCommand();

        await service.HandleStockReservationFailedAsync(
            command,
            CancellationToken.None);

        var notification = await dbContext.Notifications.SingleAsync();

        Assert.Equal(command.MessageId, notification.SourceEventId);
        Assert.Equal(command.EventType, notification.SourceEventType);
        Assert.Equal(command.CustomerEmail, notification.Recipient);
        Assert.Contains(command.OrderId.ToString(), notification.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stock reservation failed", notification.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(command.Reason!, notification.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(NotificationStatus.Created, notification.Status);
        Assert.Equal(UtcNow, notification.CreatedAtUtc);

        var processedMessage = await dbContext.ProcessedMessages.SingleAsync();

        Assert.Equal(command.MessageId, processedMessage.MessageId);
        Assert.Equal(command.EventType, processedMessage.EventType);
        Assert.Equal(ConsumerNames.NotificationsStockReservationFailedConsumer, processedMessage.ConsumerName);
        Assert.Equal(UtcNow, processedMessage.ProcessedAtUtc);
    }

    [Fact]
    public async Task HandleOrderCreatedAsync_WhenSameMessageIsProcessedTwice_DoesNotCreateDuplicateNotificationOrProcessedMessage()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = CreateOrderCreatedCommand();

        await service.HandleOrderCreatedAsync(
            command,
            CancellationToken.None);

        await service.HandleOrderCreatedAsync(
            command,
            CancellationToken.None);

        Assert.Equal(1, await dbContext.Notifications.CountAsync());
        Assert.Equal(1, await dbContext.ProcessedMessages.CountAsync());

        var notification = await dbContext.Notifications.SingleAsync();

        Assert.Equal(command.MessageId, notification.SourceEventId);
        Assert.Equal(command.EventType, notification.SourceEventType);
    }

    [Fact]
    public async Task HandleStockReservedAsync_WhenSameMessageIsProcessedTwice_DoesNotCreateDuplicateNotificationOrProcessedMessage()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = CreateStockReservedCommand();

        await service.HandleStockReservedAsync(
            command,
            CancellationToken.None);

        await service.HandleStockReservedAsync(
            command,
            CancellationToken.None);

        Assert.Equal(1, await dbContext.Notifications.CountAsync());
        Assert.Equal(1, await dbContext.ProcessedMessages.CountAsync());

        var notification = await dbContext.Notifications.SingleAsync();

        Assert.Equal(command.MessageId, notification.SourceEventId);
        Assert.Equal(command.EventType, notification.SourceEventType);
    }

    [Fact]
    public async Task HandleStockReservationFailedAsync_WhenSameMessageIsProcessedTwice_DoesNotCreateDuplicateNotificationOrProcessedMessage()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = CreateStockReservationFailedCommand();

        await service.HandleStockReservationFailedAsync(
            command,
            CancellationToken.None);

        await service.HandleStockReservationFailedAsync(
            command,
            CancellationToken.None);

        Assert.Equal(1, await dbContext.Notifications.CountAsync());
        Assert.Equal(1, await dbContext.ProcessedMessages.CountAsync());

        var notification = await dbContext.Notifications.SingleAsync();

        Assert.Equal(command.MessageId, notification.SourceEventId);
        Assert.Equal(command.EventType, notification.SourceEventType);
    }

    [Fact]
    public async Task HandleOrderCreatedAsync_WhenNotificationAlreadyExists_AddsProcessedMessageWithoutCreatingDuplicateNotification()
    {
        await using var dbContext = CreateDbContext();

        var command = CreateOrderCreatedCommand();

        dbContext.Notifications.Add(
            new Notification(
                id: Guid.NewGuid(),
                sourceEventId: command.MessageId,
                sourceEventType: command.EventType!,
                recipient: command.CustomerEmail!,
                subject: "Existing notification",
                body: "Existing body",
                createdAtUtc: UtcNow.AddMinutes(-10)));

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await service.HandleOrderCreatedAsync(
            command,
            CancellationToken.None);

        Assert.Equal(1, await dbContext.Notifications.CountAsync());
        Assert.Equal(1, await dbContext.ProcessedMessages.CountAsync());

        var processedMessage = await dbContext.ProcessedMessages.SingleAsync();

        Assert.Equal(command.MessageId, processedMessage.MessageId);
        Assert.Equal(ConsumerNames.NotificationsOrderCreatedConsumer, processedMessage.ConsumerName);
    }

    [Fact]
    public async Task HandleOrderCreatedAsync_WithInvalidCommand_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = new CreateOrderCreatedNotificationCommand
        {
            MessageId = Guid.Empty,
            EventType = "",
            CorrelationId = Guid.Empty,
            OrderId = Guid.Empty,
            CustomerName = "",
            CustomerEmail = "not-an-email"
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.HandleOrderCreatedAsync(
                command,
                CancellationToken.None));

        Assert.NotEmpty(exception.Errors);
        Assert.Empty(dbContext.Notifications);
        Assert.Empty(dbContext.ProcessedMessages);
    }

    [Fact]
    public async Task HandleStockReservedAsync_WithInvalidCommand_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = new CreateStockReservedNotificationCommand
        {
            MessageId = Guid.Empty,
            EventType = "",
            CorrelationId = Guid.Empty,
            OrderId = Guid.Empty,
            CustomerName = "",
            CustomerEmail = "not-an-email"
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.HandleStockReservedAsync(
                command,
                CancellationToken.None));

        Assert.NotEmpty(exception.Errors);
        Assert.Empty(dbContext.Notifications);
        Assert.Empty(dbContext.ProcessedMessages);
    }

    [Fact]
    public async Task HandleStockReservationFailedAsync_WithInvalidCommand_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = new CreateStockReservationFailedNotificationCommand
        {
            MessageId = Guid.Empty,
            EventType = "",
            CorrelationId = Guid.Empty,
            OrderId = Guid.Empty,
            CustomerName = "",
            CustomerEmail = "not-an-email",
            Reason = ""
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.HandleStockReservationFailedAsync(
                command,
                CancellationToken.None));

        Assert.NotEmpty(exception.Errors);
        Assert.Empty(dbContext.Notifications);
        Assert.Empty(dbContext.ProcessedMessages);
    }

    private static EventNotificationApplicationService CreateService(
        NotificationsDbContext dbContext)
    {
        var clock = new TestClock(UtcNow);

        var orderCreatedValidator = new CreateOrderCreatedNotificationCommandValidator();
        var stockReservedValidator = new CreateStockReservedNotificationCommandValidator();
        var stockReservationFailedValidator = new CreateStockReservationFailedNotificationCommandValidator();

        return new EventNotificationApplicationService(
            dbContext,
            clock,
            orderCreatedValidator,
            stockReservedValidator,
            stockReservationFailedValidator);
    }

    private static NotificationsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
            {
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;

        return new NotificationsDbContext(options);
    }

    private static CreateOrderCreatedNotificationCommand CreateOrderCreatedCommand()
    {
        return new CreateOrderCreatedNotificationCommand
        {
            MessageId = Guid.NewGuid(),
            EventType = IntegrationEventTypes.OrderCreated,
            CorrelationId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            CustomerName = "John Doe",
            CustomerEmail = "john.doe@example.com"
        };
    }

    private static CreateStockReservedNotificationCommand CreateStockReservedCommand()
    {
        return new CreateStockReservedNotificationCommand
        {
            MessageId = Guid.NewGuid(),
            EventType = IntegrationEventTypes.StockReserved,
            CorrelationId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            CustomerName = "John Doe",
            CustomerEmail = "john.doe@example.com"
        };
    }

    private static CreateStockReservationFailedNotificationCommand CreateStockReservationFailedCommand()
    {
        return new CreateStockReservationFailedNotificationCommand
        {
            MessageId = Guid.NewGuid(),
            EventType = IntegrationEventTypes.StockReservationFailed,
            CorrelationId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            CustomerName = "John Doe",
            CustomerEmail = "john.doe@example.com",
            Reason = "Insufficient stock."
        };
    }
}