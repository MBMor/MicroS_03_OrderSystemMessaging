using NotificationsService.Application.Common.Abstractions;

namespace NotificationsService.Application.UnitTests.Common;

public sealed class TestClock : IClock
{
    public TestClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; set; }
}