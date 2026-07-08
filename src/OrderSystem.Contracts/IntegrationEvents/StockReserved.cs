namespace OrderSystem.Contracts.IntegrationEvents;

public sealed record StockReserved : IIntegrationEvent
{
    public required Guid EventId { get; init; }

    public string EventType => IntegrationEventTypes.StockReserved;

    public required DateTime OccurredAtUtc { get; init; }

    public required string CorrelationId { get; init; }

    public required Guid OrderId { get; init; }

    public required string CustomerName { get; init; }

    public required string CustomerEmail { get; init; }

    public required IReadOnlyCollection<StockReservedItem> Items { get; init; }
}