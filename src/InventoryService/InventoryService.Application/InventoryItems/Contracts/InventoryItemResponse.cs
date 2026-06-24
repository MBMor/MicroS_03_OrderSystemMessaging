namespace InventoryService.Application.InventoryItems.Contracts;

public sealed record InventoryItemResponse
{
    public required Guid ProductId { get; init; }

    public required string ProductName { get; init; }

    public required int AvailableQuantity { get; init; }

    public required int ReservedQuantity { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}