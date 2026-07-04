using InventoryService.Api.Common;
using InventoryService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;
using Microsoft.AspNetCore.Authentication;

namespace InventoryService.Api.PostgresIntegrationTests.Common;

public sealed class InventoryApiFactory : WebApplicationFactory<ApiAssemblyMarker>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("inventorydb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public HttpClient HttpClient { get; private set; } = default!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");

        builder.ConfigureServices(services =>
        {
            // Integration tests should test HTTP + PostgreSQL.
            // RabbitMQ background workers are not needed here.
            services.RemoveAll<IHostedService>();

            // Defensive replacement, similar to MicroS_02 integration test factory.
            services.RemoveAll<DbContextOptions<InventoryDbContext>>();

            services.AddDbContext<InventoryDbContext>(options =>
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

        HttpClient = CreateAuthenticatedClient();

        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            truncate table
                "ProcessedMessages",
                "OutboxMessages",
                "StockReservationItems",
                "StockReservations",
                "InventoryItems"
            restart identity cascade;
            """);
    }

    public HttpClient CreateAuthenticatedClient(params string[] roles)
    {
        var client = CreateClient();

        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.HeaderName,
            TestAuthenticationHandler.HeaderValue);

        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(
                TestAuthenticationHandler.RolesHeaderName,
                string.Join(',', roles));
        }

        return client;
    }

    public HttpClient CreateUnauthenticatedClient()
    {
        return CreateClient();
    }

    public HttpClient CreateCustomerClient()
    {
        return CreateAuthenticatedClient("customer");
    }

    public HttpClient CreateSupportClient()
    {
        return CreateAuthenticatedClient("support");
    }

    public HttpClient CreateAdminClient()
    {
        return CreateAuthenticatedClient("admin");
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
        Environment.SetEnvironmentVariable("RabbitMQTopology__OrderCreatedQueueName", "inventory.order-created");
        Environment.SetEnvironmentVariable("RabbitMQTopology__OrderCreatedDeadLetterQueueName", "inventory.order-created.dlq");
        Environment.SetEnvironmentVariable("RabbitMQTopology__InitializationRetryDelaySeconds", "60");

        Environment.SetEnvironmentVariable("OrderCreatedConsumer__PrefetchCount", "1");
        Environment.SetEnvironmentVariable("OrderCreatedConsumer__ConnectionRetryDelaySeconds", "60");

        Environment.SetEnvironmentVariable("OutboxPublisher__BatchSize", "20");
        Environment.SetEnvironmentVariable("OutboxPublisher__PollingIntervalSeconds", "60");
        Environment.SetEnvironmentVariable("OutboxPublisher__MaxRetryCount", "5");

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
        Environment.SetEnvironmentVariable("RabbitMQTopology__OrderCreatedDeadLetterQueueName", null);
        Environment.SetEnvironmentVariable("RabbitMQTopology__InitializationRetryDelaySeconds", null);

        Environment.SetEnvironmentVariable("OrderCreatedConsumer__PrefetchCount", null);
        Environment.SetEnvironmentVariable("OrderCreatedConsumer__ConnectionRetryDelaySeconds", null);

        Environment.SetEnvironmentVariable("OutboxPublisher__BatchSize", null);
        Environment.SetEnvironmentVariable("OutboxPublisher__PollingIntervalSeconds", null);
        Environment.SetEnvironmentVariable("OutboxPublisher__MaxRetryCount", null);

        Environment.SetEnvironmentVariable("Jwt__Authority", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("Jwt__ValidIssuer", null);
        Environment.SetEnvironmentVariable("Jwt__RequireHttpsMetadata", null);
    }
}