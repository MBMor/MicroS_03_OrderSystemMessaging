namespace InventoryService.Infrastructure.Outbox;

public enum OutboxStatus
{
    Pending = 1,
    Published = 2,
    Failed = 3
}