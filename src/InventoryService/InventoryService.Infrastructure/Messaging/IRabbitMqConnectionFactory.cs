using RabbitMQ.Client;

namespace InventoryService.Infrastructure.Messaging;

public interface IRabbitMqConnectionFactory
{
    Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken);
}