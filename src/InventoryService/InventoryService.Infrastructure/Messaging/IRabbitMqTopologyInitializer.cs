namespace InventoryService.Infrastructure.Messaging;

public interface IRabbitMqTopologyInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}