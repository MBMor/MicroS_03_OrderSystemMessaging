using FluentValidation;
using Microsoft.EntityFrameworkCore;
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
    IValidator<MarkOrderStockReservationFailedCommand> stockReservationFailedValidator) : IOrderStockReservationResultService
{
    private readonly OrdersDbContext _dbContext = dbContext;
    private readonly IClock _clock = clock;
    private readonly IValidator<MarkOrderStockReservedCommand> _stockReservedValidator = stockReservedValidator;
    private readonly IValidator<MarkOrderStockReservationFailedCommand> 
        _stockReservationFailedValidator = stockReservationFailedValidator;

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
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(
                order => order.Id == command.OrderId,
                cancellationToken);

        if (order is null)
        {
            throw new OrderNotFoundException(command.OrderId);
        }

        if (order.Status == OrderStatus.StockReserved)
        {
            AddProcessedMessage(
                command.MessageId,
                command.EventType!,
                ConsumerNames.OrdersStockReservedConsumer);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return;
        }

        order.MarkStockReserved(_clock.UtcNow);

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
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(
                order => order.Id == command.OrderId,
                cancellationToken);

        if (order is null)
        {
            throw new OrderNotFoundException(command.OrderId);
        }

        if (order.Status == OrderStatus.StockReservationFailed)
        {
            AddProcessedMessage(
                command.MessageId,
                command.EventType!,
                ConsumerNames.OrdersStockReservationFailedConsumer);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return;
        }

        order.MarkStockReservationFailed(_clock.UtcNow);

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