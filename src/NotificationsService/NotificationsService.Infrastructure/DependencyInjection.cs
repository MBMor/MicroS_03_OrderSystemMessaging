using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationsService.Application.Common.Abstractions;
using NotificationsService.Application.Notifications.Abstractions;
using NotificationsService.Infrastructure.Notifications;
using NotificationsService.Infrastructure.Persistence;
using NotificationsService.Infrastructure.Time;

namespace NotificationsService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Notifications database connection string is not configured.");
        }

        services.AddDbContext<NotificationsDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<INotificationsService, NotificationsApplicationService>();

        return services;
    }
}