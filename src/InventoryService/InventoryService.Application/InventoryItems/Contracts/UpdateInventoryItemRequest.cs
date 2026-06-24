namespace InventoryService.Application.InventoryItems.Contracts;

public sealed record UpdateInventoryItemRequest
{
    public string? ProductName { get; init; }

    public int AvailableQuantity { get; init; }
}