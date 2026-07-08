using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrderSystem.Contracts.IntegrationEvents;
using OrdersService.Application.Common.Exceptions;
using OrdersService.Application.StockReservations.Contracts;
using OrdersService.Application.StockReservations.Validation;
using OrdersService.Application.UnitTests.Common;
using OrdersService.Domain.Orders;
using OrdersService.Infrastructure.Messaging;
using OrdersService.Infrastructure.Persistence;
using OrdersService.Infrastructure.StockReservations;
using Microsoft.Extensions.Logging.Abstractions;

namespace OrdersService.Application.UnitTests.StockReservations;

public sealed class OrderStockReservationResultApplicationServiceTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleStockReservedAsync_WhenOrderExists_ChangesOrderStatusToStockReserved()
    {
        await using var dbContext = CreateDbContext();

        var order = CreatePendingOrder();

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var command = CreateStockReservedCommand(order.Id);

        await service.HandleStockReservedAsync(
            command,
            CancellationToken.None);

        var updatedOrder = await dbContext.Orders.SingleAsync();

        Assert.Equal(OrderStatus.StockReserved, updatedOrder.Status);
        Assert.Equal(UtcNow, updatedOrder.UpdatedAtUtc);

        var processedMessage = await dbContext.ProcessedMessages.SingleAsync();

        Assert.Equal(command.MessageId, processedMessage.MessageId);
        Assert.Equal(command.EventType, processedMessage.EventType);
        Assert.Equal(ConsumerNames.OrdersStockReservedConsumer, processedMessage.ConsumerName);
    }

    [Fact]
    public async Task HandleStockReservationFailedAsync_WhenOrderExists_ChangesOrderStatusToStockReservationFailed()
    {
        await using var dbContext = CreateDbContext();

        var order = CreatePendingOrder();

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var command = CreateStockReservationFailedCommand(order.Id);

        await service.HandleStockReservationFailedAsync(
            command,
            CancellationToken.None);

        var updatedOrder = await dbContext.Orders.SingleAsync();

        Assert.Equal(OrderStatus.StockReservationFailed, updatedOrder.Status);
        Assert.Equal(UtcNow, updatedOrder.UpdatedAtUtc);

        var processedMessage = await dbContext.ProcessedMessages.SingleAsync();

        Assert.Equal(command.MessageId, processedMessage.MessageId);
        Assert.Equal(command.EventType, processedMessage.EventType);
        Assert.Equal(ConsumerNames.OrdersStockReservationFailedConsumer, processedMessage.ConsumerName);
    }

    [Fact]
    public async Task HandleStockReservedAsync_WhenSameMessageIsProcessedTwice_DoesNotCreateDuplicateProcessedMessage()
    {
        await using var dbContext = CreateDbContext();

        var order = CreatePendingOrder();

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var command = CreateStockReservedCommand(order.Id);

        await service.HandleStockReservedAsync(
            command,
            CancellationToken.None);

        await service.HandleStockReservedAsync(
            command,
            CancellationToken.None);

        var updatedOrder = await dbContext.Orders.SingleAsync();

        Assert.Equal(OrderStatus.StockReserved, updatedOrder.Status);
        Assert.Equal(1, await dbContext.ProcessedMessages.CountAsync());

        var processedMessage = await dbContext.ProcessedMessages.SingleAsync();

        Assert.Equal(command.MessageId, processedMessage.MessageId);
        Assert.Equal(ConsumerNames.OrdersStockReservedConsumer, processedMessage.ConsumerName);
    }

    [Fact]
    public async Task HandleStockReservationFailedAsync_WhenSameMessageIsProcessedTwice_DoesNotCreateDuplicateProcessedMessage()
    {
        await using var dbContext = CreateDbContext();

        var order = CreatePendingOrder();

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var command = CreateStockReservationFailedCommand(order.Id);

        await service.HandleStockReservationFailedAsync(
            command,
            CancellationToken.None);

        await service.HandleStockReservationFailedAsync(
            command,
            CancellationToken.None);

        var updatedOrder = await dbContext.Orders.SingleAsync();

        Assert.Equal(OrderStatus.StockReservationFailed, updatedOrder.Status);
        Assert.Equal(1, await dbContext.ProcessedMessages.CountAsync());

        var processedMessage = await dbContext.ProcessedMessages.SingleAsync();

        Assert.Equal(command.MessageId, processedMessage.MessageId);
        Assert.Equal(ConsumerNames.OrdersStockReservationFailedConsumer, processedMessage.ConsumerName);
    }

    [Fact]
    public async Task HandleStockReservedAsync_WhenOrderIsAlreadyStockReservedWithDifferentMessageId_AddsProcessedMessageWithoutChangingStatusAgain()
    {
        await using var dbContext = CreateDbContext();

        var order = CreatePendingOrder();
        order.MarkStockReserved(UtcNow.AddMinutes(-5));

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var command = CreateStockReservedCommand(
            orderId: order.Id,
            messageId: Guid.NewGuid());

        await service.HandleStockReservedAsync(
            command,
            CancellationToken.None);

        var updatedOrder = await dbContext.Orders.SingleAsync();

        Assert.Equal(OrderStatus.StockReserved, updatedOrder.Status);
        Assert.Equal(UtcNow.AddMinutes(-5), updatedOrder.UpdatedAtUtc);
        Assert.Equal(1, await dbContext.ProcessedMessages.CountAsync());
    }

    [Fact]
    public async Task HandleStockReservationFailedAsync_WhenOrderIsAlreadyStockReservationFailedWithDifferentMessageId_AddsProcessedMessageWithoutChangingStatusAgain()
    {
        await using var dbContext = CreateDbContext();

        var order = CreatePendingOrder();
        order.MarkStockReservationFailed(UtcNow.AddMinutes(-5));

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var command = CreateStockReservationFailedCommand(
            orderId: order.Id,
            messageId: Guid.NewGuid());

        await service.HandleStockReservationFailedAsync(
            command,
            CancellationToken.None);

        var updatedOrder = await dbContext.Orders.SingleAsync();

        Assert.Equal(OrderStatus.StockReservationFailed, updatedOrder.Status);
        Assert.Equal(UtcNow.AddMinutes(-5), updatedOrder.UpdatedAtUtc);
        Assert.Equal(1, await dbContext.ProcessedMessages.CountAsync());
    }

    [Fact]
    public async Task HandleStockReservedAsync_WhenOrderDoesNotExist_ThrowsOrderNotFoundException()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = CreateStockReservedCommand(Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<OrderNotFoundException>(() =>
            service.HandleStockReservedAsync(
                command,
                CancellationToken.None));

        Assert.Equal(command.OrderId, exception.OrderId);
        Assert.Empty(dbContext.ProcessedMessages);
    }

    [Fact]
    public async Task HandleStockReservationFailedAsync_WhenOrderDoesNotExist_ThrowsOrderNotFoundException()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = CreateStockReservationFailedCommand(Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<OrderNotFoundException>(() =>
            service.HandleStockReservationFailedAsync(
                command,
                CancellationToken.None));

        Assert.Equal(command.OrderId, exception.OrderId);
        Assert.Empty(dbContext.ProcessedMessages);
    }

    [Fact]
    public async Task HandleStockReservedAsync_WithInvalidCommand_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = new MarkOrderStockReservedCommand
        {
            MessageId = Guid.Empty,
            EventType = "",
            CorrelationId = null,
            OrderId = Guid.Empty
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.HandleStockReservedAsync(
                command,
                CancellationToken.None));

        Assert.NotEmpty(exception.Errors);
        Assert.Empty(dbContext.ProcessedMessages);
    }

    [Fact]
    public async Task HandleStockReservationFailedAsync_WithInvalidCommand_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var command = new MarkOrderStockReservationFailedCommand
        {
            MessageId = Guid.Empty,
            EventType = "",
            CorrelationId = null,
            OrderId = Guid.Empty,
            Reason = ""
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.HandleStockReservationFailedAsync(
                command,
                CancellationToken.None));

        Assert.NotEmpty(exception.Errors);
        Assert.Empty(dbContext.ProcessedMessages);
    }

    private static OrderStockReservationResultApplicationService CreateService(
        OrdersDbContext dbContext)
    {
        var clock = new TestClock(UtcNow);

        var stockReservedValidator = new MarkOrderStockReservedCommandValidator();
        var stockReservationFailedValidator = new MarkOrderStockReservationFailedCommandValidator();

        return new OrderStockReservationResultApplicationService(
            dbContext,
            clock,
            stockReservedValidator,
            stockReservationFailedValidator,
            NullLogger<OrderStockReservationResultApplicationService>.Instance);
    }

    private static OrdersDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
            {
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;

        return new OrdersDbContext(options);
    }

    private static Order CreatePendingOrder()
    {
        var orderId = Guid.NewGuid();

        var orderItem = new OrderItem(
            id: Guid.NewGuid(),
            orderId: orderId,
            productId: Guid.NewGuid(),
            productName: "Keyboard",
            quantity: 2);

        return new Order(
            id: orderId,
            customerName: "John Doe",
            customerEmail: "john.doe@example.com",
            items: [orderItem],
            createdAtUtc: UtcNow.AddMinutes(-10));
    }

    private static MarkOrderStockReservedCommand CreateStockReservedCommand(
        Guid orderId,
        Guid? messageId = null)
    {
        return new MarkOrderStockReservedCommand
        {
            MessageId = messageId ?? Guid.NewGuid(),
            EventType = IntegrationEventTypes.StockReserved,
            CorrelationId = Guid.NewGuid().ToString("N"),
            OrderId = orderId
        };
    }

    private static MarkOrderStockReservationFailedCommand CreateStockReservationFailedCommand(
        Guid orderId,
        Guid? messageId = null)
    {
        return new MarkOrderStockReservationFailedCommand
        {
            MessageId = messageId ?? Guid.NewGuid(),
            EventType = IntegrationEventTypes.StockReservationFailed,
            CorrelationId = Guid.NewGuid().ToString("N"),
            OrderId = orderId,
            Reason = "Insufficient stock."
        };
    }
}