using RabbitMQ.Client;

namespace NotificationsService.Infrastructure.Messaging;

public interface IRabbitMqConnectionFactory
{
    Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken);
}