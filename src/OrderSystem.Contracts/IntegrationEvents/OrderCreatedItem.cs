namespace OrderSystem.Contracts.IntegrationEvents;

public sealed record OrderCreatedItem
{
    public required Guid ProductId { get; init; }

    public required string ProductName { get; init; }

    public required int Quantity { get; init; }
}