using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace NotificationsService.Infrastructure.Messaging;

public sealed class RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options) : IRabbitMqConnectionFactory
{
    private readonly RabbitMqOptions _options = options.Value;

    public Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionFactory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            ClientProvidedName = "notifications-service"
        };

        return connectionFactory.CreateConnectionAsync(cancellationToken);
    }
}