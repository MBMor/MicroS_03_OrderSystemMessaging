using NotificationsService.Application.Common.Abstractions;

namespace NotificationsService.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}