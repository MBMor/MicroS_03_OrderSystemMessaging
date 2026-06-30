using InventoryService.Application.Common.Abstractions;

namespace InventoryService.Application.UnitTests.Common;

public sealed class TestClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}