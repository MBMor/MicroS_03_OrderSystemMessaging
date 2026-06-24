namespace OrdersService.Application.Orders.Contracts;

public sealed record OrderResponse
{
    public required Guid Id { get; init; }

    public required string CustomerName { get; init; }

    public required string CustomerEmail { get; init; }

    public required string Status { get; init; }

    public required IReadOnlyCollection<OrderItemResponse> Items { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}