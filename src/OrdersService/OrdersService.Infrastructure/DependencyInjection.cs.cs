using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrdersService.Application.Common.Abstractions;
using OrdersService.Application.Orders.Abstractions;
using OrdersService.Infrastructure.Orders;
using OrdersService.Infrastructure.Persistence;
using OrdersService.Infrastructure.Time;

namespace OrdersService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Orders database connection string is not configured.");
        }

        services.AddDbContext<OrdersDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IOrdersService, OrdersApplicationService>();

        return services;
    }
}