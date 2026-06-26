namespace InventoryService.Application.StockReservations.Contracts;

public sealed record ReserveStockForOrderCommand
{
    public Guid MessageId { get; init; }

    public string? EventType { get; init; }

    public Guid CorrelationId { get; init; }

    public Guid OrderId { get; init; }

    public string? CustomerName { get; init; }

    public string? CustomerEmail { get; init; }

    public IReadOnlyCollection<ReserveStockForOrderItemCommand>? Items { get; init; }
}