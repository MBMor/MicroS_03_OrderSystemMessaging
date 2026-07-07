using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrdersService.Application.Common.Abstractions;
using OrdersService.Application.Common.Exceptions;
using OrdersService.Application.StockReservations.Abstractions;
using OrdersService.Application.StockReservations.Contracts;
using OrdersService.Domain.Orders;
using OrdersService.Infrastructure.Idempotency;
using OrdersService.Infrastructure.Messaging;
using OrdersService.Infrastructure.Persistence;

namespace OrdersService.Infrastructure.StockReservations;

public sealed class OrderStockReservationResultApplicationService(
    OrdersDbContext dbContext,
    IClock clock,
    IValidator<MarkOrderStockReservedCommand> stockReservedValidator,
    IValidator<MarkOrderStockReservationFailedCommand> stockReservationFailedValidator,
    ILogger<OrderStockReservationResultApplicationService> logger) : IOrderStockReservationResultService
{
    private readonly OrdersDbContext _dbContext = dbContext;
    private readonly IClock _clock = clock;
    private readonly IValidator<MarkOrderStockReservedCommand> _stockReservedValidator = stockReservedValidator;
    private readonly IValidator<MarkOrderStockReservationFailedCommand> _stockReservationFailedValidator = stockReservationFailedValidator;
    private readonly ILogger<OrderStockReservationResultApplicationService> _logger = logger;

    public async Task HandleStockReservedAsync(
        MarkOrderStockReservedCommand command,
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
            ConsumerNames.OrdersStockReservedConsumer,
            cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Duplicate stock reserved message {MessageId} skipped by {ConsumerName}",
                command.MessageId,
                ConsumerNames.OrdersStockReservedConsumer);

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(
                order => order.Id == command.OrderId,
                cancellationToken);

        if (order is null)
        {
            _logger.LogWarning(
                "Order {OrderId} was not found while processing stock reserved message {MessageId}",
                command.OrderId,
                command.MessageId);

            throw new OrderNotFoundException(command.OrderId);
        }

        if (order.Status == OrderStatus.StockReserved)
        {
            _logger.LogInformation(
                "Order {OrderId} already has status {OrderStatus}. Message {MessageId} will be marked as processed",
                order.Id,
                order.Status,
                command.MessageId);

            AddProcessedMessage(
                command.MessageId,
                command.EventType!,
                ConsumerNames.OrdersStockReservedConsumer);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        order.MarkStockReserved(_clock.UtcNow);

        _logger.LogInformation(
            "Order {OrderId} status changed to {OrderStatus} after message {MessageId}",
            order.Id,
            OrderStatus.StockReserved,
            command.MessageId);

        AddProcessedMessage(
            command.MessageId,
            command.EventType!,
            ConsumerNames.OrdersStockReservedConsumer);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task HandleStockReservationFailedAsync(
        MarkOrderStockReservationFailedCommand command,
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
            ConsumerNames.OrdersStockReservationFailedConsumer,
            cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Duplicate stock reservation failed message {MessageId} skipped by {ConsumerName}",
                command.MessageId,
                ConsumerNames.OrdersStockReservationFailedConsumer);

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(
                order => order.Id == command.OrderId,
                cancellationToken);

        if (order is null)
        {
            _logger.LogWarning(
                "Order {OrderId} was not found while processing stock reservation failed message {MessageId}",
                command.OrderId,
                command.MessageId);

            throw new OrderNotFoundException(command.OrderId);
        }

        if (order.Status == OrderStatus.StockReservationFailed)
        {
            _logger.LogInformation(
                "Order {OrderId} already has status {OrderStatus}. Message {MessageId} will be marked as processed",
                order.Id,
                order.Status,
                command.MessageId);

            AddProcessedMessage(
                command.MessageId,
                command.EventType!,
                ConsumerNames.OrdersStockReservationFailedConsumer);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        order.MarkStockReservationFailed(_clock.UtcNow);

        _logger.LogWarning(
            "Order {OrderId} status changed to {OrderStatus} after message {MessageId}. Reason: {Reason}",
            order.Id,
            OrderStatus.StockReservationFailed,
            command.MessageId,
            command.Reason);

        AddProcessedMessage(
            command.MessageId,
            command.EventType!,
            ConsumerNames.OrdersStockReservationFailedConsumer);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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