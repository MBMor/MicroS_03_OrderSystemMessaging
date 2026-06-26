using System.Data;
using System.Text.Json;
using FluentValidation;
using InventoryService.Application.Common.Abstractions;
using InventoryService.Application.StockReservations.Abstractions;
using InventoryService.Application.StockReservations.Contracts;
using InventoryService.Domain.Inventory;
using InventoryService.Domain.StockReservations;
using InventoryService.Infrastructure.Idempotency;
using InventoryService.Infrastructure.Messaging;
using InventoryService.Infrastructure.Outbox;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrderSystem.Contracts.IntegrationEvents;

namespace InventoryService.Infrastructure.StockReservations;

public sealed class StockReservationApplicationService(
    InventoryDbContext dbContext,
    IClock clock,
    IValidator<ReserveStockForOrderCommand> validator) : IStockReservationService
{
    private const string InsufficientStockReason = "Insufficient stock for one or more order items.";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly InventoryDbContext _dbContext = dbContext;
    private readonly IClock _clock = clock;
    private readonly IValidator<ReserveStockForOrderCommand> _validator = validator;

    public async Task HandleOrderCreatedAsync(
        ReserveStockForOrderCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationResult = await _validator.ValidateAsync(
            command,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var alreadyProcessed = await _dbContext.ProcessedMessages
            .AnyAsync(
                processedMessage =>
                    processedMessage.MessageId == command.MessageId
                    && processedMessage.EventType == command.EventType!
                    && processedMessage.ConsumerName == ConsumerNames.InventoryOrderCreatedConsumer,
                cancellationToken);

        if (alreadyProcessed)
        {
            return;
        }

        var existingReservation = await _dbContext.StockReservations
            .AsNoTracking()
            .AnyAsync(
                reservation => reservation.OrderId == command.OrderId,
                cancellationToken);

        if (existingReservation)
        {
            AddProcessedMessage(command);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return;
        }

        var now = _clock.UtcNow;

        var requestedItems = command.Items!
            .GroupBy(item => item.ProductId)
            .Select(group => new RequestedStockItem(
                ProductId: group.Key,
                Quantity: group.Sum(item => item.Quantity)))
            .ToList();

        var productIds = requestedItems
            .Select(item => item.ProductId)
            .ToList();

        var inventoryItems = await _dbContext.InventoryItems
            .Where(inventoryItem => productIds.Contains(inventoryItem.ProductId))
            .ToListAsync(cancellationToken);

        var inventoryItemsByProductId = inventoryItems.ToDictionary(
            inventoryItem => inventoryItem.ProductId);

        var failedItems = GetFailedItems(
            requestedItems,
            inventoryItemsByProductId);

        if (failedItems.Count > 0)
        {
            await CreateFailedReservationAsync(
                command,
                requestedItems,
                failedItems,
                now,
                cancellationToken);
        }
        else
        {
            await CreateSuccessfulReservationAsync(
                command,
                requestedItems,
                inventoryItemsByProductId,
                now,
                cancellationToken);
        }

        AddProcessedMessage(command);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CreateSuccessfulReservationAsync(
        ReserveStockForOrderCommand command,
        IReadOnlyCollection<RequestedStockItem> requestedItems,
        IReadOnlyDictionary<Guid, InventoryItem> inventoryItemsByProductId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var requestedItem in requestedItems)
        {
            var inventoryItem = inventoryItemsByProductId[requestedItem.ProductId];

            inventoryItem.Reserve(
                requestedItem.Quantity,
                now);
        }

        var reservationId = Guid.NewGuid();

        var reservationItems = requestedItems
            .Select(item => new StockReservationItem(
                id: Guid.NewGuid(),
                stockReservationId: reservationId,
                productId: item.ProductId,
                quantity: item.Quantity))
            .ToList();

        var reservation = StockReservation.CreateReserved(
            id: reservationId,
            orderId: command.OrderId,
            items: reservationItems,
            createdAtUtc: now);

        var integrationEvent = new StockReserved
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = now,
            CorrelationId = command.CorrelationId,
            OrderId = command.OrderId,
            CustomerName = command.CustomerName!,
            CustomerEmail = command.CustomerEmail!,
            Items = requestedItems
                .Select(item => new StockReservedItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                })
                .ToList()
        };

        var outboxMessage = CreateOutboxMessage(
            integrationEvent,
            RabbitMqRoutingKeys.StockReserved);

        _dbContext.StockReservations.Add(reservation);
        _dbContext.OutboxMessages.Add(outboxMessage);

        await Task.CompletedTask;
    }

    private async Task CreateFailedReservationAsync(
        ReserveStockForOrderCommand command,
        IReadOnlyCollection<RequestedStockItem> requestedItems,
        IReadOnlyCollection<StockReservationFailedItem> failedItems,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var reservationId = Guid.NewGuid();

        var reservationItems = requestedItems
            .Select(item => new StockReservationItem(
                id: Guid.NewGuid(),
                stockReservationId: reservationId,
                productId: item.ProductId,
                quantity: item.Quantity))
            .ToList();

        var reservation = StockReservation.CreateFailed(
            id: reservationId,
            orderId: command.OrderId,
            failureReason: InsufficientStockReason,
            items: reservationItems,
            createdAtUtc: now);

        var integrationEvent = new StockReservationFailed
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = now,
            CorrelationId = command.CorrelationId,
            OrderId = command.OrderId,
            CustomerName = command.CustomerName!,
            CustomerEmail = command.CustomerEmail!,
            Reason = InsufficientStockReason,
            Items = failedItems.ToList()
        };

        var outboxMessage = CreateOutboxMessage(
            integrationEvent,
            RabbitMqRoutingKeys.StockReservationFailed);

        _dbContext.StockReservations.Add(reservation);
        _dbContext.OutboxMessages.Add(outboxMessage);

        await Task.CompletedTask;
    }

    private void AddProcessedMessage(ReserveStockForOrderCommand command)
    {
        var processedMessage = new ProcessedMessage(
            id: Guid.NewGuid(),
            messageId: command.MessageId,
            eventType: command.EventType!,
            consumerName: ConsumerNames.InventoryOrderCreatedConsumer,
            processedAtUtc: _clock.UtcNow);

        _dbContext.ProcessedMessages.Add(processedMessage);
    }

    private static List<StockReservationFailedItem> GetFailedItems(
        IReadOnlyCollection<RequestedStockItem> requestedItems,
        IReadOnlyDictionary<Guid, InventoryItem> inventoryItemsByProductId)
    {
        var failedItems = new List<StockReservationFailedItem>();

        foreach (var requestedItem in requestedItems)
        {
            if (!inventoryItemsByProductId.TryGetValue(
                    requestedItem.ProductId,
                    out var inventoryItem))
            {
                failedItems.Add(new StockReservationFailedItem
                {
                    ProductId = requestedItem.ProductId,
                    RequestedQuantity = requestedItem.Quantity,
                    AvailableQuantity = 0
                });

                continue;
            }

            if (!inventoryItem.CanReserve(requestedItem.Quantity))
            {
                failedItems.Add(new StockReservationFailedItem
                {
                    ProductId = requestedItem.ProductId,
                    RequestedQuantity = requestedItem.Quantity,
                    AvailableQuantity = inventoryItem.AvailableQuantity
                });
            }
        }

        return failedItems;
    }

    private static OutboxMessage CreateOutboxMessage(
        IIntegrationEvent integrationEvent,
        string routingKey)
    {
        var payload = JsonSerializer.Serialize(
            integrationEvent,
            integrationEvent.GetType(),
            JsonSerializerOptions);

        return new OutboxMessage(
            id: Guid.NewGuid(),
            eventId: integrationEvent.EventId,
            eventType: integrationEvent.EventType,
            routingKey: routingKey,
            payload: payload,
            occurredAtUtc: integrationEvent.OccurredAtUtc);
    }

    private sealed record RequestedStockItem(
        Guid ProductId,
        int Quantity);
}