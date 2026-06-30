using OrdersService.Application.Common.Abstractions;

namespace OrdersService.Application.UnitTests.Common;

public sealed class TestClock : IClock
{
    public TestClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; set; }
}