namespace OrdersService.Application.Orders.Contracts;

public sealed record CreateOrderRequest
{
    public string? CustomerName { get; init; }

    public string? CustomerEmail { get; init; }

    public IReadOnlyCollection<CreateOrderItemRequest>? Items { get; init; }
}