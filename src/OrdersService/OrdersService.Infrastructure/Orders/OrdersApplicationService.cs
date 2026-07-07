using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrdersService.Application.Common.Abstractions;
using OrdersService.Application.Common.Pagination;
using OrdersService.Application.Orders.Abstractions;
using OrdersService.Application.Orders.Contracts;
using OrdersService.Domain.Orders;
using OrdersService.Infrastructure.Messaging;
using OrdersService.Infrastructure.Outbox;
using OrdersService.Infrastructure.Persistence;
using OrderSystem.Contracts.IntegrationEvents;

namespace OrdersService.Infrastructure.Orders;

public sealed class OrdersApplicationService(
    OrdersDbContext dbContext,
    IClock clock,
    IValidator<CreateOrderRequest> createOrderRequestValidator,
    IValidator<ListOrdersRequest> listOrdersRequestValidator,
    ILogger<OrdersApplicationService> logger) : IOrdersService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly OrdersDbContext _dbContext = dbContext;
    private readonly IClock _clock = clock;
    private readonly IValidator<CreateOrderRequest> _createOrderRequestValidator = createOrderRequestValidator;
    private readonly IValidator<ListOrdersRequest> _listOrdersRequestValidator = listOrdersRequestValidator;
    private readonly ILogger<OrdersApplicationService> _logger = logger;

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

        _logger.LogInformation(
            "Creating order {OrderId} with {ItemCount} items",
            order.Id,
            order.Items.Count);

        _logger.LogInformation(
            "OrderCreated outbox message {OutboxMessageId} prepared for order {OrderId}. EventId: {EventId}, EventType: {EventType}, RoutingKey: {RoutingKey}",
            outboxMessage.Id,
            order.Id,
            outboxMessage.EventId,
            outboxMessage.EventType,
            outboxMessage.RoutingKey);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.Orders.Add(order);
        _dbContext.OutboxMessages.Add(outboxMessage);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} created and outbox message {OutboxMessageId} stored",
            order.Id,
            outboxMessage.Id);

        return MapToResponse(order);
    }

    public async Task<OrderResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(
                order => order.Id == id,
                cancellationToken);

        return order is null
            ? null
            : MapToResponse(order);
    }

    public async Task<PagedResult<OrderResponse>> ListAsync(
        ListOrdersRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _listOrdersRequestValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var query = _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = Enum.Parse<OrderStatus>(
                request.Status,
                ignoreCase: true);

            query = query.Where(order => order.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(
            query,
            request.SortBy!,
            request.SortDirection!);

        var orders = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = orders
            .Select(MapToResponse)
            .ToList();

        return new PagedResult<OrderResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
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

    private static IQueryable<Order> ApplySorting(
        IQueryable<Order> query,
        string sortBy,
        string sortDirection)
    {
        var descending = string.Equals(
            sortDirection,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLowerInvariant() switch
        {
            "createdatutc" => descending
                ? query.OrderByDescending(order => order.CreatedAtUtc)
                : query.OrderBy(order => order.CreatedAtUtc),

            "updatedatutc" => descending
                ? query.OrderByDescending(order => order.UpdatedAtUtc)
                : query.OrderBy(order => order.UpdatedAtUtc),

            "customername" => descending
                ? query.OrderByDescending(order => order.CustomerName)
                : query.OrderBy(order => order.CustomerName),

            "status" => descending
                ? query.OrderByDescending(order => order.Status)
                : query.OrderBy(order => order.Status),

            _ => descending
                ? query.OrderByDescending(order => order.CreatedAtUtc)
                : query.OrderBy(order => order.CreatedAtUtc)
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