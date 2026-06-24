using System.Text.Json;
using FluentValidation;
using OrderSystem.Contracts.IntegrationEvents;
using OrdersService.Application.Common.Abstractions;
using OrdersService.Application.Common.Pagination;
using OrdersService.Application.Orders.Abstractions;
using OrdersService.Application.Orders.Contracts;
using OrdersService.Domain.Orders;
using OrdersService.Infrastructure.Messaging;
using OrdersService.Infrastructure.Outbox;
using OrdersService.Infrastructure.Persistence;

namespace OrdersService.Infrastructure.Orders;

public sealed class OrdersApplicationService(
    OrdersDbContext dbContext,
    IClock clock,
    IValidator<CreateOrderRequest> createOrderRequestValidator) : IOrdersService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly OrdersDbContext _dbContext = dbContext;
    private readonly IClock _clock = clock;
    private readonly IValidator<CreateOrderRequest> _createOrderRequestValidator = createOrderRequestValidator;

    public async Task<OrderResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _createOrderRequestValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var orderId = Guid.NewGuid();
        var now = _clock.UtcNow;

        var orderItems = request.Items!
            .Select(item => new OrderItem(
                id: Guid.NewGuid(),
                orderId: orderId,
                productId: item.ProductId,
                productName: item.ProductName!,
                quantity: item.Quantity))
            .ToList();

        var order = new Order(
            id: orderId,
            customerName: request.CustomerName!,
            customerEmail: request.CustomerEmail!,
            items: orderItems,
            createdAtUtc: now);

        var orderCreatedEvent = CreateOrderCreatedEvent(order, now);

        var payload = JsonSerializer.Serialize(
            orderCreatedEvent,
            JsonSerializerOptions);

        var outboxMessage = new OutboxMessage(
            id: Guid.NewGuid(),
            eventId: orderCreatedEvent.EventId,
            eventType: orderCreatedEvent.EventType,
            routingKey: RabbitMqRoutingKeys.OrderCreated,
            payload: payload,
            occurredAtUtc: orderCreatedEvent.OccurredAtUtc);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.Orders.Add(order);
        _dbContext.OutboxMessages.Add(outboxMessage);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return MapToResponse(order);
    }

    public Task<OrderResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Get order by ID will be implemented in step 24.");
    }

    public Task<PagedResult<OrderResponse>> ListAsync(
        ListOrdersRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("List orders will be implemented in step 25.");
    }

    private static OrderCreated CreateOrderCreatedEvent(Order order, DateTime occurredAtUtc)
    {
        return new OrderCreated
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = Guid.NewGuid(),
            OrderId = order.Id,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            Items = order.Items
                .Select(item => new OrderCreatedItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity
                })
                .ToList()
        };
    }

    private static OrderResponse MapToResponse(Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            Status = order.Status.ToString(),
            CreatedAtUtc = order.CreatedAtUtc,
            UpdatedAtUtc = order.UpdatedAtUtc,
            Items = order.Items
                .Select(item => new OrderItemResponse
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity
                })
                .ToList()
        };
    }
}