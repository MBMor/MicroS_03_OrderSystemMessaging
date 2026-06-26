using InventoryService.Application.Common.Abstractions;
using InventoryService.Application.InventoryItems.Abstractions;
using InventoryService.Application.StockReservations.Abstractions;
using InventoryService.Infrastructure.InventoryItems;
using InventoryService.Infrastructure.Messaging;
using InventoryService.Infrastructure.Persistence;
using InventoryService.Infrastructure.StockReservations;
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

        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.HostName), "RabbitMQ HostName is required.")
            .Validate(options => options.Port > 0, "RabbitMQ Port must be greater than 0.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.UserName), "RabbitMQ UserName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "RabbitMQ Password is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ExchangeName), "RabbitMQ ExchangeName is required.")
            .ValidateOnStart();

        services
            .AddOptions<RabbitMqTopologyOptions>()
            .Bind(configuration.GetSection(RabbitMqTopologyOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.DeadLetterExchangeName), "RabbitMQTopology DeadLetterExchangeName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.OrderCreatedQueueName), "RabbitMQTopology OrderCreatedQueueName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.OrderCreatedDeadLetterQueueName), "RabbitMQTopology OrderCreatedDeadLetterQueueName is required.")
            .Validate(options => options.InitializationRetryDelaySeconds > 0, "RabbitMQTopology InitializationRetryDelaySeconds must be greater than 0.")
            .ValidateOnStart();

        services.AddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();
        services.AddSingleton<IRabbitMqTopologyInitializer, RabbitMqTopologyInitializer>();
        services.AddHostedService<RabbitMqTopologyInitializerBackgroundService>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IInventoryItemsService, InventoryItemsApplicationService>();
        services.AddScoped<IStockReservationService, StockReservationApplicationService>();

        return services;
    }
}