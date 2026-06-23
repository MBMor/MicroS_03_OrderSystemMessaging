using InventoryService.Application.Common.Abstractions;

namespace InventoryService.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}