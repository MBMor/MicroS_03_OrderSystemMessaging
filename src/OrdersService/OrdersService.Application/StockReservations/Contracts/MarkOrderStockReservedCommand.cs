namespace OrdersService.Application.StockReservations.Contracts;

public sealed record MarkOrderStockReservedCommand
{
    public Guid MessageId { get; init; }

    public string? EventType { get; init; }

    public Guid CorrelationId { get; init; }

    public Guid OrderId { get; init; }
}