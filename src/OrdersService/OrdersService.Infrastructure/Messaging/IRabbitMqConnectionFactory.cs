using RabbitMQ.Client;

namespace OrdersService.Infrastructure.Messaging;

public interface IRabbitMqConnectionFactory
{
    Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken);
}