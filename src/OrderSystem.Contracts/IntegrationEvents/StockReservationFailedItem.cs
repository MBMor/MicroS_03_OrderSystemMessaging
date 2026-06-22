namespace OrderSystem.Contracts.IntegrationEvents;

public sealed record StockReservationFailedItem
{
    public required Guid ProductId { get; init; }

    public required int RequestedQuantity { get; init; }

    public required int AvailableQuantity { get; init; }
}