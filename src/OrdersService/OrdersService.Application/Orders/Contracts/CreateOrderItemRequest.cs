namespace OrdersService.Application.Orders.Contracts;

public sealed record CreateOrderItemRequest
{
    public Guid ProductId { get; init; }

    public string? ProductName { get; init; }

    public int Quantity { get; init; }
}