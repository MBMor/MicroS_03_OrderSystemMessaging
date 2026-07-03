using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NotificationsService.Api.Common;
using NotificationsService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;
using Microsoft.AspNetCore.Authentication;

namespace NotificationsService.Api.PostgresIntegrationTests.Common;

public sealed class NotificationsApiFactory : WebApplicationFactory<ApiAssemblyMarker>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("notificationsdb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public HttpClient HttpClient { get; private set; } = default!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");

        builder.ConfigureServices(services =>
        {
            // These API + PostgreSQL integration tests should not depend on RabbitMQ background workers.
            services.RemoveAll<IHostedService>();

            // Replace the DbContext registration from Program.cs / infrastructure registration.
            services.RemoveAll<DbContextOptions<NotificationsDbContext>>();

            services.AddDbContext<NotificationsDbContext>(options =>
            {
                options.UseNpgsql(_postgresContainer.GetConnectionString());
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.AuthenticationScheme;
                    options.DefaultScheme = TestAuthenticationHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    _ => { });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        SetEnvironmentVariables();

        HttpClient = CreateClient();

        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            truncate table
                "ProcessedMessages",
                "Notifications"
            restart identity cascade;
            """);
    }

    public new async Task DisposeAsync()
    {
        HttpClient.Dispose();

        ClearEnvironmentVariables();

        await _postgresContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    private void SetEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            _postgresContainer.GetConnectionString());

        Environment.SetEnvironmentVariable("RabbitMQ__HostName", "localhost");
        Environment.SetEnvironmentVariable("RabbitMQ__Port", "5672");
        Environment.SetEnvironmentVariable("RabbitMQ__UserName", "guest");
        Environment.SetEnvironmentVariable("RabbitMQ__Password", "guest");
        Environment.SetEnvironmentVariable("RabbitMQ__ExchangeName", "ordersystem.events");

        Environment.SetEnvironmentVariable("RabbitMQTopology__DeadLetterExchangeName", "ordersystem.events.dlx");
        Environment.SetEnvironmentVariable("RabbitMQTopology__OrderCreatedQueueName", "notifications.order-created");
        Environment.SetEnvironmentVariable("RabbitMQTopology__StockReservedQueueName", "notifications.stock-reserved");
        Environment.SetEnvironmentVariable("RabbitMQTopology__StockReservationFailedQueueName", "notifications.stock-reservation-failed");
        Environment.SetEnvironmentVariable("RabbitMQTopology__OrderCreatedDeadLetterQueueName", "notifications.order-created.dlq");
        Environment.SetEnvironmentVariable("RabbitMQTopology__StockReservedDeadLetterQueueName", "notifications.stock-reserved.dlq");
        Environment.SetEnvironmentVariable("RabbitMQTopology__StockReservationFailedDeadLetterQueueName", "notifications.stock-reservation-failed.dlq");
        Environment.SetEnvironmentVariable("RabbitMQTopology__InitializationRetryDelaySeconds", "60");

        Environment.SetEnvironmentVariable("EventNotificationConsumers__PrefetchCount", "1");
        Environment.SetEnvironmentVariable("EventNotificationConsumers__ConnectionRetryDelaySeconds", "60");

        Environment.SetEnvironmentVariable("Jwt__Authority", "http://localhost:18080/realms/order-system");
        Environment.SetEnvironmentVariable("Jwt__Audience", "order-system-api");
        Environment.SetEnvironmentVariable("Jwt__ValidIssuer", "http://localhost:18080/realms/order-system");
        Environment.SetEnvironmentVariable("Jwt__RequireHttpsMetadata", "false");
    }

    private static void ClearEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);

        Environment.SetEnvironmentVariable("RabbitMQ__HostName", null);
        Environment.SetEnvironmentVariable("RabbitMQ__Port", null);
        Environment.SetEnvironmentVariable("RabbitMQ__UserName", null);
        Environment.SetEnvironmentVariable("RabbitMQ__Password", null);
        Environment.SetEnvironmentVariable("RabbitMQ__ExchangeName", null);

        Environment.SetEnvironmentVariable("RabbitMQTopology__DeadLetterExchangeName", null);
        Environment.SetEnvironmentVariable("RabbitMQTopology__OrderCreatedQueueName", null);
        Environment.SetEnvironmentVariable("RabbitMQTopology__StockReservedQueueName", null);
        Environment.SetEnvironmentVariable("RabbitMQTopology__StockReservationFailedQueueName", null);
        Environment.SetEnvironmentVariable("RabbitMQTopology__OrderCreatedDeadLetterQueueName", null);
        Environment.SetEnvironmentVariable("RabbitMQTopology__StockReservedDeadLetterQueueName", null);
        Environment.SetEnvironmentVariable("RabbitMQTopology__StockReservationFailedDeadLetterQueueName", null);
        Environment.SetEnvironmentVariable("RabbitMQTopology__InitializationRetryDelaySeconds", null);

        Environment.SetEnvironmentVariable("EventNotificationConsumers__PrefetchCount", null);
        Environment.SetEnvironmentVariable("EventNotificationConsumers__ConnectionRetryDelaySeconds", null);

        Environment.SetEnvironmentVariable("Jwt__Authority", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("Jwt__ValidIssuer", null);
        Environment.SetEnvironmentVariable("Jwt__RequireHttpsMetadata", null);
    }
}