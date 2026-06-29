namespace OrdersService.Application.StockReservations.Contracts;

public sealed record MarkOrderStockReservationFailedCommand
{
    public Guid MessageId { get; init; }

    public string? EventType { get; init; }

    public Guid CorrelationId { get; init; }

    public Guid OrderId { get; init; }

    public string? Reason { get; init; }
}