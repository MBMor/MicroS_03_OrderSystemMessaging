namespace NotificationsService.Infrastructure.Messaging;

public interface IRabbitMqTopologyInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}