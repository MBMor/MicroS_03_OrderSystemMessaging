namespace InventoryService.Application.Common.Exceptions;

public sealed class InventoryItemNotFoundException(Guid productId) 
    : Exception($"Inventory item for product ID '{productId}' was not found.")
{
    public Guid ProductId { get; } = productId;
}