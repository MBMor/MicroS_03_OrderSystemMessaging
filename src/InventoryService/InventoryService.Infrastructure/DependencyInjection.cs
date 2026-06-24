using InventoryService.Application.Common.Abstractions;
using InventoryService.Application.InventoryItems.Abstractions;
using InventoryService.Infrastructure.InventoryItems;
using InventoryService.Infrastructure.Persistence;
using InventoryService.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Inventory database connection string is not configured.");
        }

        services.AddDbContext<InventoryDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IInventoryItemsService, InventoryItemsApplicationService>();

        return services;
    }
}