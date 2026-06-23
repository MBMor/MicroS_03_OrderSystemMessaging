using InventoryService.Domain.Common;

namespace InventoryService.Domain.Inventory;

public sealed class InvalidStockOperationException(string message) : DomainException(message)
{
}