using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrdersService.Application.Common.Abstractions;
using OrdersService.Application.Orders.Abstractions;
using OrdersService.Infrastructure.Messaging;
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

        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.HostName), "RabbitMQ HostName is required.")
            .Validate(options => options.Port > 0, "RabbitMQ Port must be greater than 0.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.UserName), "RabbitMQ UserName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "RabbitMQ Password is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ExchangeName), "RabbitMQ ExchangeName is required.")
            .ValidateOnStart();

        services.AddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();
        services.AddSingleton<IRabbitMqTopologyInitializer, RabbitMqTopologyInitializer>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IOrdersService, OrdersApplicationService>();

        return services;
    }
}