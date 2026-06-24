namespace InventoryService.Application.InventoryItems.Contracts;

public sealed record CreateInventoryItemRequest
{
    public Guid ProductId { get; init; }

    public string? ProductName { get; init; }

    public int AvailableQuantity { get; init; }
}