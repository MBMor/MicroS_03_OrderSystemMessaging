namespace OrderSystem.Contracts.IntegrationEvents;

public interface IIntegrationEvent
{
    Guid EventId { get; }

    string EventType { get; }

    DateTime OccurredAtUtc { get; }

    Guid CorrelationId { get; }
}