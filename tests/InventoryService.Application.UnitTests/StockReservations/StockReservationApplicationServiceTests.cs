using FluentValidation;
using InventoryService.Application.StockReservations.Contracts;
using InventoryService.Application.StockReservations.Validation;
using InventoryService.Application.UnitTests.Common;
using InventoryService.Domain.Inventory;
using InventoryService.Domain.StockReservations;
using InventoryService.Infrastructure.Messaging;
using InventoryService.Infrastructure.Outbox;
using InventoryService.Infrastructure.Persistence;
using InventoryService.Infrastructure.StockReservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrderSystem.Contracts.IntegrationEvents;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryService.Application.UnitTests.StockReservations;

public sealed class StockReservationApplicationServiceTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleOrderCreatedAsync_WithEnoughStock_ReservesStockAndCreatesStockReservedOutboxMessage()
    {
        await using var dbContext = CreateDbContext();

        var productId = Guid.NewGuid();

        dbContext.InventoryItems.Add(CreateInventoryItem(productId, availableQuantity: 50));

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var command = CreateCommand(
            productId: productId,
            quantity: 2);

        await service.HandleOrderCreatedAsync(
            command,
            CancellationToken.None);

        var inventoryItem = await dbContext.InventoryItems.SingleAsync();

        Assert.Equal(48, inventoryItem.AvailableQuantity);
        Assert.Equal(2, inventoryItem.ReservedQuantity);

        var reservation = await dbContext.StockReservations
            .Include(stockReservation => stockReservation.Items)
            .SingleAsync();

        Assert.Equal(command.OrderId, reservation.OrderId);
        Assert.Equal(StockReservationStatus.Reserved, reservation.Status);
        Assert.Null(reservation.FailureReason);
        Assert.Single(reservation.Items);

        var outboxMessage = await dbContext.OutboxMessages.SingleAsync();

        Assert.Equal(IntegrationEventTypes.StockReserved, outboxMessage.EventType);
        Assert.Equal(RabbitMqRoutingKeys.StockReserved, outboxMessage.RoutingKey);
        Assert.Equal(OutboxStatus.Pending, outboxMessage.Status);
        Assert.Contains(command.OrderId.ToString(), outboxMessage.Payload, StringComparison.OrdinalIgnoreCase);

        var processedMessage = await dbContext.ProcessedMessages.SingleAsync();

        Assert.Equal(command.MessageId, processedMessage.MessageId);
        Assert.Equal(command.EventType, processedMessage.EventType);
        Assert.Equal(ConsumerNames.InventoryOrderCreatedConsumer, processedMessage.ConsumerName);
    }

    [Fact]
    public async Task HandleOrderCreatedAsync_WithInsufficientStock_CreatesFailedReservationAndStockReservationFailedOutboxMessage()
    {
        await using var dbContext = CreateDbContext();

        var productId = Guid.NewGuid();

        dbContext.InventoryItems.Add(CreateInventoryItem(productId, availableQuantity: 1));

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var command = CreateCommand(
            productId: productId,
            quantity: 2);

        await service.HandleOrderCreatedAsync(
            command,
            CancellationToken.None);

        var inventoryItem = await dbContext.InventoryItems.SingleAsync();

        Assert.Equal(1, inventoryItem.AvailableQuantity);
        Assert.Equal(0, inventoryItem.ReservedQuantity);

        var reservation = await dbContext.StockReservations
            .Include(stockReservation => stockReservation.Items)
            .SingleAsync();

        Assert.Equal(command.OrderId, reservation.OrderId);
        Assert.Equal(StockReservationStatus.Failed, reservation.Status);
        Assert.NotNull(reservation.FailureReason);
        Assert.Single(reservation.Items);

        var outboxMessage = await dbContext.OutboxMessages.SingleAsync();

        Assert.Equal(IntegrationEventTypes.StockReservationFailed, outboxMessage.EventType);
        Assert.Equal(RabbitMqRoutingKeys.StockReservationFailed, outboxMessage.RoutingKey);
        Assert.Equal(OutboxStatus.Pending, outboxMessage.Status);
        Assert.Contains(command.OrderId.ToString(), outboxMessage.Payload, StringComparison.OrdinalIgnoreCase);

        var processedMessage = await dbContext.ProcessedMessages.SingleAsync();

        Assert.Equal(command.MessageId, processedMessage.MessageId);
        Assert.Equal(command.EventType, processedMessage.EventType);
        Assert.Equal(ConsumerNames.InventoryOrderCreatedConsumer, processedMessage.ConsumerName);
    }

    [Fact]
    public async Task HandleOrderCreatedAsync_WithMissingProduct_CreatesFailedReservation()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = CreateCommand(
            productId: Guid.NewGuid(),
            quantity: 2);

        await service.HandleOrderCreatedAsync(
            command,
            CancellationToken.None);

        Assert.Empty(dbContext.InventoryItems);

        var reservation = await dbContext.StockReservations.SingleAsync();

        Assert.Equal(command.OrderId, reservation.OrderId);
        Assert.Equal(StockReservationStatus.Failed, reservation.Status);

        var outboxMessage = await dbContext.OutboxMessages.SingleAsync();

        Assert.Equal(IntegrationEventTypes.StockReservationFailed, outboxMessage.EventType);
        Assert.Equal(RabbitMqRoutingKeys.StockReservationFailed, outboxMessage.RoutingKey);
    }

    [Fact]
    public async Task HandleOrderCreatedAsync_WhenSameMessageIsProcessedTwice_DoesNotCreateDuplicateReservationOrOutboxMessage()
    {
        await using var dbContext = CreateDbContext();

        var productId = Guid.NewGuid();

        dbContext.InventoryItems.Add(CreateInventoryItem(productId, availableQuantity: 50));

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var command = CreateCommand(
            productId: productId,
            quantity: 2);

        await service.HandleOrderCreatedAsync(
            command,
            CancellationToken.None);

        await service.HandleOrderCreatedAsync(
            command,
            CancellationToken.None);

        var inventoryItem = await dbContext.InventoryItems.SingleAsync();

        Assert.Equal(48, inventoryItem.AvailableQuantity);
        Assert.Equal(2, inventoryItem.ReservedQuantity);

        Assert.Equal(1, await dbContext.StockReservations.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        Assert.Equal(1, await dbContext.ProcessedMessages.CountAsync());
    }

    [Fact]
    public async Task HandleOrderCreatedAsync_WhenSameOrderIdIsProcessedWithDifferentMessageId_DoesNotCreateDuplicateReservation()
    {
        await using var dbContext = CreateDbContext();

        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        dbContext.InventoryItems.Add(CreateInventoryItem(productId, availableQuantity: 50));

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var firstCommand = CreateCommand(
            productId: productId,
            quantity: 2,
            orderId: orderId,
            messageId: Guid.NewGuid());

        var secondCommand = CreateCommand(
            productId: productId,
            quantity: 2,
            orderId: orderId,
            messageId: Guid.NewGuid());

        await service.HandleOrderCreatedAsync(
            firstCommand,
            CancellationToken.None);

        await service.HandleOrderCreatedAsync(
            secondCommand,
            CancellationToken.None);

        var inventoryItem = await dbContext.InventoryItems.SingleAsync();

        Assert.Equal(48, inventoryItem.AvailableQuantity);
        Assert.Equal(2, inventoryItem.ReservedQuantity);

        Assert.Equal(1, await dbContext.StockReservations.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        Assert.Equal(2, await dbContext.ProcessedMessages.CountAsync());
    }

    [Fact]
    public async Task HandleOrderCreatedAsync_WithInvalidCommand_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = new ReserveStockForOrderCommand
        {
            MessageId = Guid.Empty,
            EventType = "",
            CorrelationId = Guid.Empty,
            OrderId = Guid.Empty,
            CustomerName = "",
            CustomerEmail = "not-an-email",
            Items = []
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.HandleOrderCreatedAsync(
                command,
                CancellationToken.None));

        Assert.NotEmpty(exception.Errors);
        Assert.Empty(dbContext.StockReservations);
        Assert.Empty(dbContext.OutboxMessages);
        Assert.Empty(dbContext.ProcessedMessages);
    }

    private static StockReservationApplicationService CreateService(
        InventoryDbContext dbContext)
    {
        var clock = new TestClock(UtcNow);
        var validator = new ReserveStockForOrderCommandValidator();

        return new StockReservationApplicationService(
            dbContext,
            clock,
            validator,
            NullLogger<StockReservationApplicationService>.Instance);
    }

    private static InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
            {
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;

        return new InventoryDbContext(options);
    }

    private static InventoryItem CreateInventoryItem(
        Guid productId,
        int availableQuantity)
    {
        return new InventoryItem(
            id: Guid.NewGuid(),
            productId: productId,
            productName: "Keyboard",
            availableQuantity: availableQuantity,
            createdAtUtc: UtcNow);
    }

    private static ReserveStockForOrderCommand CreateCommand(
        Guid productId,
        int quantity,
        Guid? orderId = null,
        Guid? messageId = null)
    {
        return new ReserveStockForOrderCommand
        {
            MessageId = messageId ?? Guid.NewGuid(),
            EventType = IntegrationEventTypes.OrderCreated,
            CorrelationId = Guid.NewGuid(),
            OrderId = orderId ?? Guid.NewGuid(),
            CustomerName = "John Doe",
            CustomerEmail = "john.doe@example.com",
            Items =
            [
                new ReserveStockForOrderItemCommand
                {
                    ProductId = productId,
                    Quantity = quantity
                }
            ]
        };
    }
}