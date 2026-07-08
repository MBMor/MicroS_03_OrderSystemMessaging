namespace OrderSystem.Contracts.IntegrationEvents;

public sealed record OrderCreated : IIntegrationEvent
{
    public required Guid EventId { get; init; }

    public string EventType => IntegrationEventTypes.OrderCreated;

    public required DateTime OccurredAtUtc { get; init; }

    public required string CorrelationId { get; init; }

    public required Guid OrderId { get; init; }

    public required string CustomerName { get; init; }

    public required string CustomerEmail { get; init; }

    public required IReadOnlyCollection<OrderCreatedItem> Items { get; init; }
}