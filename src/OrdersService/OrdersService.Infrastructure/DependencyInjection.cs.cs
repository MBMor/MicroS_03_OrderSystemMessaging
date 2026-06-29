using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrdersService.Application.Common.Abstractions;
using OrdersService.Application.Orders.Abstractions;
using OrdersService.Application.StockReservations.Abstractions;
using OrdersService.Infrastructure.Messaging;
using OrdersService.Infrastructure.Orders;
using OrdersService.Infrastructure.Outbox;
using OrdersService.Infrastructure.Persistence;
using OrdersService.Infrastructure.StockReservations;
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

        services
            .AddOptions<RabbitMqTopologyOptions>()
            .Bind(configuration.GetSection(RabbitMqTopologyOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.DeadLetterExchangeName), "RabbitMQTopology DeadLetterExchangeName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.StockReservedQueueName), "RabbitMQTopology StockReservedQueueName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.StockReservedDeadLetterQueueName), "RabbitMQTopology StockReservedDeadLetterQueueName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.StockReservationFailedQueueName), "RabbitMQTopology StockReservationFailedQueueName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.StockReservationFailedDeadLetterQueueName), "RabbitMQTopology StockReservationFailedDeadLetterQueueName is required.")
            .Validate(options => options.InitializationRetryDelaySeconds > 0, "RabbitMQTopology InitializationRetryDelaySeconds must be greater than 0.")
            .ValidateOnStart();

        services
            .AddOptions<OutboxPublisherOptions>()
            .Bind(configuration.GetSection(OutboxPublisherOptions.SectionName))
            .Validate(options => options.BatchSize > 0, "OutboxPublisher BatchSize must be greater than 0.")
            .Validate(options => options.PollingIntervalSeconds > 0, "OutboxPublisher PollingIntervalSeconds must be greater than 0.")
            .Validate(options => options.MaxRetryCount > 0, "OutboxPublisher MaxRetryCount must be greater than 0.")
            .ValidateOnStart();

        services
            .AddOptions<StockReservationResultConsumerOptions>()
            .Bind(configuration.GetSection(StockReservationResultConsumerOptions.SectionName))
            .Validate(options => options.PrefetchCount > 0, "StockReservationResultConsumers PrefetchCount must be greater than 0.")
            .Validate(options => options.ConnectionRetryDelaySeconds > 0, "StockReservationResultConsumers ConnectionRetryDelaySeconds must be greater than 0.")
            .ValidateOnStart();

        services.AddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();
        services.AddSingleton<IRabbitMqTopologyInitializer, RabbitMqTopologyInitializer>();

        services.AddHostedService<RabbitMqTopologyInitializerBackgroundService>();
        services.AddHostedService<OrdersOutboxPublisherBackgroundService>();
        services.AddHostedService<StockReservedConsumerBackgroundService>();
        services.AddHostedService<StockReservationFailedConsumerBackgroundService>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IOrdersService, OrdersApplicationService>();
        services.AddScoped<IOrderStockReservationResultService, OrderStockReservationResultApplicationService>();

        return services;
    }
}