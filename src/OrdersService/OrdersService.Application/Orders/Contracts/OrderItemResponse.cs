namespace OrdersService.Application.Orders.Contracts;

public sealed record OrderItemResponse
{
    public required Guid ProductId { get; init; }

    public required string ProductName { get; init; }

    public required int Quantity { get; init; }
}