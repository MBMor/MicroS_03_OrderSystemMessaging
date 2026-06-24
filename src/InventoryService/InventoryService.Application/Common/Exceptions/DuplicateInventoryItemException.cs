namespace InventoryService.Application.Common.Exceptions;

public sealed class DuplicateInventoryItemException(Guid productId) 
    : Exception($"Inventory item for product ID '{productId}' already exists.")
{
    public Guid ProductId { get; } = productId;
}