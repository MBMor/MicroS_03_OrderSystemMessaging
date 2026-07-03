using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OrdersService.Api.Common;
using OrdersService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;

namespace OrdersService.Api.PostgresIntegrationTests.Common;

public sealed class OrdersApiFactory : WebApplicationFactory<ApiAssemblyMarker>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("ordersdb")
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
            services.RemoveAll<DbContextOptions<OrdersDbContext>>();

            services.AddDbContext<OrdersDbContext>(options =>
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

        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            truncate table
                "ProcessedMessages",
                "OutboxMessages",
                "OrderItems",
                "Orders"
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

        Environment.SetEnvironmentVariable("OutboxPublisher__BatchSize", null);
        Environment.SetEnvironmentVariable("OutboxPublisher__PollingIntervalSeconds", null);
        Environment.SetEnvironmentVariable("OutboxPublisher__MaxRetryCount", null);

        Environment.SetEnvironmentVariable("Jwt__Authority", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("Jwt__ValidIssuer", null);
        Environment.SetEnvironmentVariable("Jwt__RequireHttpsMetadata", null);
    }
}