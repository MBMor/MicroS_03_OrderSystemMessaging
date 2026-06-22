namespace OrderSystem.Contracts.IntegrationEvents;

public sealed record StockReservedItem
{
    public required Guid ProductId { get; init; }

    public required int Quantity { get; init; }
}