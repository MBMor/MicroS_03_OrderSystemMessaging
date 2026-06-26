namespace InventoryService.Application.StockReservations.Contracts;

public sealed record ReserveStockForOrderItemCommand
{
    public Guid ProductId { get; init; }

    public int Quantity { get; init; }
}